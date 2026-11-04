#!/usr/bin/env python3
"""Tests the finished installers on a CI runner (ADR 0010); run scripts/build.py first.

It really installs and uninstalls Somnus and the .NET Desktop Runtime, so it refuses to run outside
CI, where the PC is thrown away afterwards. Every Setup runs silently.

1. Each installer goes over a fake self-contained install: the old runtime files disappear, Somnus
   does not start with Windows, it passes its self-test in the language of the installer, and the
   uninstaller removes it together with Start with Windows (#83).
2. A Setup compiled with ForceRuntimeInstall downloads the runtime, checks its SHA-256 and starts
   Microsoft's installer.
3. The same Setup with a wrong SHA-256 stops, and nothing gets installed.

Usage (in the workflows):
    python scripts/test_installer.py --version 1.1.0
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import tempfile
import time
import winreg
from pathlib import Path

import build

SETUP_TIMEOUT_SECONDS = 600
UNINSTALL_TIMEOUT_SECONDS = 60
OLD_RUNTIME_FILES = ["hostfxr.dll", "coreclr.dll", "createdump.exe", "Somnus.pdb", "pl/PresentationCore.resources.dll"]
NEW_FILES = ["Somnus.exe", "Somnus.dll", "README.txt", "LICENSE.txt", "THIRD_PARTY_LICENSES.txt", "pl/Somnus.Presentation.resources.dll"]
WRONG_SHA256 = "0" * 64
MAXIMUM_INSTALLED_MB = 10
RUN_KEY = r"Software\Microsoft\Windows\CurrentVersion\Run"
STARTUP_APPROVED_KEY = r"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
AUTOSTART_VALUE = "Somnus"


class InstallerTestError(Exception):
    """A check failed; the message says which one."""


def require(condition: bool, message: str) -> None:
    """Stop the test run when a check fails."""
    if not condition:
        raise InstallerTestError(message)


def run_setup(setup: Path, folder: Path) -> tuple[int, str]:
    """Run a Setup silently into a folder and return its exit code and its log."""
    log = folder.parent / f"{folder.name}.log"
    command = [str(setup), "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", f"/DIR={folder}", f"/LOG={log}"]
    code = subprocess.run(command, check=False, timeout=SETUP_TIMEOUT_SECONDS).returncode
    return code, log.read_text(encoding="utf-8", errors="replace") if log.exists() else ""


def run_self_test(folder: Path) -> None:
    """Run the self-test of an installed Somnus."""
    report = folder.parent / f"{folder.name}-self-test.json"
    command = [str(folder / "Somnus.exe"), "--self-test", str(report)]
    code = subprocess.run(command, check=False, timeout=build.SELF_TEST_TIMEOUT_SECONDS).returncode
    require(code == 0, f"The self-test failed in {folder}: {report.read_text(encoding='utf-8') if report.exists() else 'no report'}")


def uninstall(folder: Path) -> None:
    """Run the uninstaller, which hands over to a copy of itself, and wait until Somnus is gone."""
    subprocess.run([str(folder / "unins000.exe"), "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART"], check=True)
    deadline = time.monotonic() + UNINSTALL_TIMEOUT_SECONDS
    while (folder / "Somnus.exe").exists() and time.monotonic() < deadline:
        time.sleep(1)
    require(not (folder / "Somnus.exe").exists(), f"The uninstaller left Somnus in {folder}.")


def run_value(key: str) -> object | None:
    """Return the value named Somnus under a key of HKEY_CURRENT_USER, or None."""
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, key) as handle:
            return winreg.QueryValueEx(handle, AUTOSTART_VALUE)[0]
    except FileNotFoundError:
        return None


def turn_on_autostart(folder: Path) -> None:
    """Do what Start with Windows does, plus Task Manager's flag, so the uninstaller has both to remove."""
    with winreg.CreateKey(winreg.HKEY_CURRENT_USER, RUN_KEY) as run:
        winreg.SetValueEx(run, AUTOSTART_VALUE, 0, winreg.REG_SZ, f'"{folder / "Somnus.exe"}" --autostart')
    with winreg.CreateKey(winreg.HKEY_CURRENT_USER, STARTUP_APPROVED_KEY) as approved:
        winreg.SetValueEx(approved, AUTOSTART_VALUE, 0, winreg.REG_BINARY, bytes([3] + [0] * 11))


def test_install_over_a_bundled_runtime(version: str, language: str, work: Path) -> None:
    """Install over a fake self-contained build, run the app and uninstall it."""
    print(f"== Install, self-test and uninstall ({build.LANGUAGES[language]})", flush=True)
    folder = work / f"upgrade-{language}"
    for name in OLD_RUNTIME_FILES:
        (folder / name).parent.mkdir(parents=True, exist_ok=True)
        (folder / name).write_bytes(b"old")

    code, log = run_setup(build.INSTALLER_DIR / f"Somnus-Setup-{version}-{language}.exe", folder)

    require(code == 0, f"Setup failed with exit code {code}:\n{log}")
    for name in OLD_RUNTIME_FILES:
        require(not (folder / name).exists(), f"Setup left the old file {name}.")
    for name in NEW_FILES:
        require((folder / name).exists(), f"Setup did not install {name}.")
    ini = (folder / "Somnus.ini").read_text(encoding="utf-16" if (folder / "Somnus.ini").read_bytes()[:2] == b"\xff\xfe" else "utf-8")
    require(f"Language={build.LANGUAGES[language]}" in ini, f"Somnus.ini does not name the language: {ini}")
    size_mb = sum(path.stat().st_size for path in folder.rglob("*") if path.is_file()) / 1_000_000
    print(f"Installed size: {size_mb:.1f} MB", flush=True)
    require(size_mb <= MAXIMUM_INSTALLED_MB, f"Somnus takes {size_mb:.1f} MB after installation.")
    require(run_value(RUN_KEY) is None, "Setup made Somnus start with Windows without the user's choice.")
    run_self_test(folder)
    turn_on_autostart(folder)
    uninstall(folder)
    require(run_value(RUN_KEY) is None, "The uninstaller left Somnus in the startup apps.")
    require(run_value(STARTUP_APPROVED_KEY) is None, "The uninstaller left Task Manager's flag for Somnus.")


def compile_forced_setup(version: str, inno_compiler: Path, sha256: str, name: str, work: Path) -> Path:
    """Compile a Setup that takes the path of a PC without the runtime."""
    runtime = build.RuntimeDownload(**json.loads(build.RUNTIME_CACHE.read_text(encoding="utf-8")))
    command = [
        str(inno_compiler), "/Q", "/DForceRuntimeInstall=1", "/DLang=en",
        f"/DAppVersion={version}", f"/DFileVersion={version.split('-')[0]}",
        f"/DRuntimeVersion={runtime.version}", f"/DRuntimeUrl={runtime.url}", f"/DRuntimeSha256={sha256}",
        f"/O{work}", f"/F{name}", str(build.ROOT / "installer" / "Somnus.iss"),
    ]
    subprocess.run(command, check=True)
    return work / f"{name}.exe"


def test_runtime_download(version: str, inno_compiler: Path, work: Path) -> None:
    """A Setup on a PC without the runtime gets it from Microsoft and goes on."""
    print("== Download and install the .NET Desktop Runtime", flush=True)
    runtime = build.RuntimeDownload(**json.loads(build.RUNTIME_CACHE.read_text(encoding="utf-8")))
    setup = compile_forced_setup(version, inno_compiler, runtime.sha256, "forced-runtime", work)
    folder = work / "forced-runtime-app"

    code, log = run_setup(setup, folder)

    require(code == 0, f"Setup failed with exit code {code}:\n{log}")
    require("The runtime could not be installed" not in log and "did not start" not in log, f"The runtime step failed:\n{log}")
    require((folder / "Somnus.exe").exists(), "Setup did not go on after the runtime.")
    run_self_test(folder)
    uninstall(folder)


def test_wrong_checksum(version: str, inno_compiler: Path, work: Path) -> None:
    """A download that fails the SHA-256 is never started, and Setup stops without installing."""
    print("== Reject a runtime download with a wrong SHA-256", flush=True)
    setup = compile_forced_setup(version, inno_compiler, WRONG_SHA256, "wrong-checksum", work)
    folder = work / "wrong-checksum-app"

    code, log = run_setup(setup, folder)

    require(code != 0, f"Setup went on after a wrong checksum:\n{log}")
    require("The runtime could not be installed" in log, f"Setup did not report the failed download:\n{log}")
    require(not (folder / "Somnus.exe").exists(), "Setup installed Somnus after a wrong checksum.")


def main() -> int:
    """Run the checks, and turn a failure into one readable message."""
    parser = argparse.ArgumentParser(description="Tests the finished installers on a CI runner.")
    parser.add_argument("--version", default="0.0.0-local", help="the version that scripts/build.py built")
    parser.add_argument("--inno-compiler", help="the path of ISCC.exe")
    options = parser.parse_args()

    if not os.environ.get("CI"):
        print("This test installs Somnus and .NET for real, so it runs only in CI.", file=sys.stderr)
        return 1

    inno_compiler = build.find_inno_compiler(options.inno_compiler)
    with tempfile.TemporaryDirectory(prefix="somnus-installer-test-") as temporary:
        work = Path(temporary)
        try:
            for language in build.LANGUAGES:
                test_install_over_a_bundled_runtime(options.version, language, work)
            test_runtime_download(options.version, inno_compiler, work)
            test_wrong_checksum(options.version, inno_compiler, work)
        except InstallerTestError as error:
            print(f"Installer test failed: {error}", file=sys.stderr)
            return 1
    print("All installer checks passed.", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
