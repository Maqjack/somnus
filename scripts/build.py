#!/usr/bin/env python3
"""Builds the Somnus downloads from a clean state (SWPA 3.14, ADR 0009, ADR 0010).

1. Publishes src/Somnus.App as a framework-dependent win-x64 folder.
2. Writes THIRD_PARTY_LICENSES.txt and adds it to the folder.
3. Writes README.txt and LICENSE.txt for each language.
4. Puts together the portable folder of each language, with a Somnus.ini that names the language.
5. Runs Somnus.exe --self-test in each portable folder.
6. Checks the portable folders: forbidden content, the manifest and the size budget in docs/.
7. Compiles installer/Somnus.iss once per language with the current .NET Desktop Runtime download
   (not with --skip-installers).
8. Zips the portable folders and writes SHA256SUMS.txt.
9. With --backup, keeps a copy of the build through scripts/rotate_backups.py (SWPA 3.15).

Every failure stops the build with exit code 1. The release workflow runs this on a clean checkout;
it also works locally for a test build, but official releases come only from GitHub Actions. It needs
Windows, the .NET SDK and, for the installers, Inno Setup 6.

Usage:
    py -3 scripts/build.py --version 1.1.0
    py -3 scripts/build.py --skip-installers
    py -3 scripts/build.py --update-manifest
    py -3 scripts/build.py --version 1.1.0 --backup
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
import urllib.request
import zipfile
from collections.abc import Callable, Iterable
from dataclasses import dataclass
from pathlib import Path
from typing import TypeVar

ROOT = Path(__file__).resolve().parent.parent
ARTIFACTS = ROOT / "artifacts"
PUBLISH_DIR = ARTIFACTS / "publish"
INSTALLER_DIR = ARTIFACTS / "installer"
IMAGES_DIR = ARTIFACTS / "installer-images"
RELEASE_DIR = ARTIFACTS / "release"
NOTICES = ARTIFACTS / "THIRD_PARTY_LICENSES.txt"
RUNTIME_CACHE = ARTIFACTS / "runtime-download.json"
BUILT_MANIFEST = ARTIFACTS / "build-manifest-win-x64.txt"
MANIFEST = ROOT / "docs" / "build-manifest-win-x64.txt"
BUDGET = ROOT / "docs" / "build-budget.txt"

# The code in the file names (ADR 0009) and the installer's name of the language, which Somnus.ini holds.
LANGUAGES = {"en": "english", "pl": "polish"}

VERSION_PATTERN = re.compile(r"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$")
RUNTIME_METADATA_URL = "https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json"
RUNTIME_FILE_NAME = "windowsdesktop-runtime-win-x64.exe"
NETWORK_TIMEOUT_SECONDS = 120
NETWORK_ATTEMPTS = 4
SELF_TEST_TIMEOUT_SECONDS = 120
BUDGET_MARGIN = 1.10

# Everything in the build has one of these extensions: code, runtime settings and the texts SWPA 3.14 allows.
ALLOWED_EXTENSIONS = {".dll", ".exe", ".json", ".ini", ".txt"}
FORBIDDEN_PATTERN = re.compile(
    r"(^|/)(tests?|docs|scripts|\.git\w*|\.vs|\.env[^/]*|__pycache__|\.\w+_cache)(/|$)"
    r"|\.tests\.dll$"
    r"|(^|/)(xunit|coverlet|testhost|microsoft\.testplatform|microsoft\.testing)[^/]*$",
    re.IGNORECASE,
)

T = TypeVar("T")


class BuildError(Exception):
    """A build step failed; the message says which one and why."""


@dataclass(frozen=True)
class RuntimeDownload:
    """The .NET Desktop Runtime installer that Setup fetches when the runtime is missing."""

    version: str
    url: str
    sha256: str


def run(description: str, *command: str | Path) -> None:
    """Run a build step and stop the build when it fails."""
    print(f"== {description}", flush=True)
    result = subprocess.run([str(part) for part in command], cwd=ROOT, check=False)
    if result.returncode != 0:
        raise BuildError(f"{description} failed with exit code {result.returncode}.")


def run_powershell(description: str, script: str, *arguments: str) -> None:
    """Run one of the PowerShell scripts in scripts/."""
    run(description, "powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ROOT / "scripts" / script, *arguments)


def with_retries(action: Callable[[], T]) -> T:
    """Run a network action, waiting longer after each failure (SWPA 3.7)."""
    for attempt in range(1, NETWORK_ATTEMPTS + 1):
        try:
            return action()
        except OSError as error:
            if attempt == NETWORK_ATTEMPTS:
                raise BuildError(f"The network request failed: {error}") from error
            print(f"Network problem ({error}); trying again.", flush=True)
            time.sleep(2**attempt)
    raise AssertionError("unreachable")


def clean() -> None:
    """Remove the output of earlier builds, so nothing old can slip into this one."""
    folders = [PUBLISH_DIR, INSTALLER_DIR, IMAGES_DIR, RELEASE_DIR]
    folders += [package_dir(language) for language in LANGUAGES]
    folders += [portable_dir(language) for language in LANGUAGES]
    for folder in folders:
        shutil.rmtree(folder, ignore_errors=True)
        if folder.exists():
            raise BuildError(f"{folder} could not be removed; is Somnus running from it?")
    for file in [NOTICES, BUILT_MANIFEST, *ARTIFACTS.glob("self-test-*.json")]:
        file.unlink(missing_ok=True)
    RELEASE_DIR.mkdir(parents=True)


def package_dir(language: str) -> Path:
    """Return the folder with README.txt and LICENSE.txt of a language."""
    return ARTIFACTS / f"package-{language}"


def portable_dir(language: str) -> Path:
    """Return the portable folder of a language."""
    return ARTIFACTS / f"portable-{language}"


def package_text(parts: Iterable[str], version: str) -> str:
    """Join text files with a line between them, in Windows line endings, with the version filled in."""
    text = f"\n\n{'-' * 72}\n\n".join(part.replace("\r\n", "\n").strip() for part in parts)
    return text.replace("{version}", version).replace("\n", "\r\n") + "\r\n"


def write_package_texts(version: str) -> None:
    """Write README.txt and LICENSE.txt for each language (ADR 0009)."""
    for language in LANGUAGES:
        print(f"== README.txt and LICENSE.txt in {LANGUAGES[language]}", flush=True)
        folder = package_dir(language)
        folder.mkdir(parents=True)
        license_sources = ["LICENSE"] if language == "en" else [f"installer/LICENSE.{language}.txt", "LICENSE"]
        texts = {"README.txt": [f"installer/README.{language}.txt"], "LICENSE.txt": license_sources}
        for name, sources in texts.items():
            parts = [(ROOT / source).read_text(encoding="utf-8") for source in sources]
            # The byte order mark lets every Notepad and the licence page of Setup show Polish letters.
            (folder / name).write_text(package_text(parts, version), encoding="utf-8-sig", newline="")


def assemble_portable_folders() -> None:
    """Combine the published files, the texts and Somnus.ini for each language (#53)."""
    for language, language_name in LANGUAGES.items():
        folder = portable_dir(language)
        shutil.copytree(PUBLISH_DIR, folder)
        for text in package_dir(language).iterdir():
            shutil.copy2(text, folder)
        (folder / "Somnus.ini").write_text(f"[Installer]\r\nLanguage={language_name}\r\n", encoding="ascii", newline="")


def run_self_test(language: str) -> None:
    """Run Somnus.exe --self-test in a portable folder and show its report (SWPA 3.14)."""
    print(f"== Self-test of the {LANGUAGES[language]} build", flush=True)
    report = ARTIFACTS / f"self-test-{language}.json"
    command = [str(portable_dir(language) / "Somnus.exe"), "--self-test", str(report)]
    try:
        result = subprocess.run(command, cwd=ROOT, check=False, timeout=SELF_TEST_TIMEOUT_SECONDS)
    except subprocess.TimeoutExpired as error:
        raise BuildError(f"The self-test did not finish in {SELF_TEST_TIMEOUT_SECONDS} seconds.") from error
    if not report.exists():
        raise BuildError(f"The self-test wrote no report (exit code {result.returncode}).")
    checks = json.loads(report.read_text(encoding="utf-8"))["checks"]
    for check in checks:
        state = "ok" if check["passed"] else "FAILED"
        print(f"{check['name']:<20} {state:<7} {check['milliseconds']:>5} ms", flush=True)
        if check["error"]:
            print(check["error"], flush=True)
    if result.returncode != 0:
        raise BuildError(f"The self-test failed with exit code {result.returncode}.")


def build_paths(folder: Path) -> list[str]:
    """Return the sorted paths of the files in a build, relative and with forward slashes."""
    return sorted(path.relative_to(folder).as_posix() for path in folder.rglob("*") if path.is_file())


def forbidden_paths(paths: Iterable[str]) -> list[str]:
    """Return the paths that must not be in a build (SWPA 3.14)."""
    return [
        path
        for path in paths
        if FORBIDDEN_PATTERN.search(path) or Path(path).suffix.lower() not in ALLOWED_EXTENSIONS
    ]


def check_contents(update_manifest: bool) -> None:
    """Check both portable folders: forbidden content, the same files and the committed manifest."""
    print("== Check the contents of the build", flush=True)
    paths = build_paths(portable_dir("en"))
    forbidden = forbidden_paths(paths)
    if forbidden:
        raise BuildError("Forbidden content in the build: " + ", ".join(forbidden))
    if build_paths(portable_dir("pl")) != paths:
        raise BuildError("The English and Polish builds hold different files.")

    manifest = "\n".join(paths) + "\n"
    BUILT_MANIFEST.write_text(manifest, encoding="utf-8", newline="\n")
    if update_manifest:
        MANIFEST.write_text(manifest, encoding="utf-8", newline="\n")
        print(f"Wrote {MANIFEST.relative_to(ROOT).as_posix()}.", flush=True)
    elif not MANIFEST.exists() or MANIFEST.read_text(encoding="utf-8") != manifest:
        raise BuildError(
            f"The build differs from {MANIFEST.relative_to(ROOT).as_posix()}. "
            f"Compare it with {BUILT_MANIFEST.relative_to(ROOT).as_posix()} and, if the change is "
            "meant, run build.py with --update-manifest and explain it in the pull request."
        )


def parse_budget(text: str) -> tuple[float, int] | None:
    """Read the size in MB and the file count from docs/build-budget.txt (SWPA 3.14)."""
    match = re.search(r"^Budget: ([\d.]+) MB, (\d+) files", text, re.MULTILINE)
    return (float(match[1]), int(match[2])) if match else None


def over_budget(size_mb: float, files: int, budget: tuple[float, int]) -> bool:
    """Tell whether a build grew more than SWPA 3.14 allows without a reason in the pull request."""
    return size_mb > budget[0] * BUDGET_MARGIN or files > budget[1] * BUDGET_MARGIN


def report_size() -> None:
    """Print the size and file count of the build and compare them with the budget."""
    files = [path for path in portable_dir("en").rglob("*") if path.is_file()]
    size_mb = sum(path.stat().st_size for path in files) / 1_000_000
    print(f"== Build size: {size_mb:.2f} MB in {len(files)} files", flush=True)
    budget = parse_budget(BUDGET.read_text(encoding="utf-8")) if BUDGET.exists() else None
    if budget is None:
        print("docs/build-budget.txt has no build budget yet.", flush=True)
    elif over_budget(size_mb, len(files), budget):
        print(f"Warning: over the budget of {budget[0]} MB and {budget[1]} files by more than 10%.", flush=True)


def fetch_json(url: str) -> dict:
    """Download a JSON document."""

    def download() -> dict:
        with urllib.request.urlopen(url, timeout=NETWORK_TIMEOUT_SECONDS) as response:
            return json.load(response)

    return with_retries(download)


def hash_download(url: str) -> tuple[str, str]:
    """Download a file without keeping it and return its SHA-256 and SHA-512."""

    def download() -> tuple[str, str]:
        sha256, sha512 = hashlib.sha256(), hashlib.sha512()
        with urllib.request.urlopen(url, timeout=NETWORK_TIMEOUT_SECONDS) as response:
            while chunk := response.read(1024 * 1024):
                sha256.update(chunk)
                sha512.update(chunk)
        return sha256.hexdigest(), sha512.hexdigest()

    return with_retries(download)


def resolve_runtime() -> RuntimeDownload:
    """Find the newest .NET Desktop Runtime 10 in Microsoft's release metadata and pin its hash.

    Setup verifies the download with SHA-256, while the metadata gives SHA-512, so the build fetches
    the file once, checks it against the metadata and passes the SHA-256 on to Inno Setup.
    """
    print("== Find the current .NET Desktop Runtime", flush=True)
    metadata = fetch_json(RUNTIME_METADATA_URL)
    release = next(item for item in metadata["releases"] if item["release-version"] == metadata["latest-release"])
    desktop = release["windowsdesktop"]
    file = next(item for item in desktop["files"] if item["name"] == RUNTIME_FILE_NAME)

    if RUNTIME_CACHE.exists():
        cached = RuntimeDownload(**json.loads(RUNTIME_CACHE.read_text(encoding="utf-8")))
        if cached.url == file["url"]:
            print(f"{cached.version}, known from an earlier build", flush=True)
            return cached

    sha256, sha512 = hash_download(file["url"])
    if sha512 != file["hash"].lower():
        raise BuildError(f"The download of {file['url']} does not match Microsoft's SHA-512.")
    runtime = RuntimeDownload(desktop["version"], file["url"], sha256)
    RUNTIME_CACHE.write_text(json.dumps(runtime.__dict__), encoding="utf-8")
    print(f"{runtime.version}: {runtime.url}", flush=True)
    return runtime


def find_inno_compiler(given: str | None) -> Path:
    """Find ISCC.exe of Inno Setup 6."""
    if given:
        return Path(given)
    candidates = [
        Path(os.environ.get("ProgramFiles(x86)", "")) / "Inno Setup 6" / "ISCC.exe",
        Path(os.environ.get("ProgramFiles", "")) / "Inno Setup 6" / "ISCC.exe",
        Path(os.environ.get("LOCALAPPDATA", "")) / "Programs" / "Inno Setup 6" / "ISCC.exe",
    ]
    for candidate in candidates:
        if candidate.is_file():
            return candidate
    raise BuildError('Inno Setup 6 was not found. Install it with "winget install JRSoftware.InnoSetup" or pass --inno-compiler.')


def compile_installers(version: str, inno_compiler: Path) -> None:
    """Compile one installer per language; the build log names the compiler and its version."""
    run_powershell("Installer images", "make-installer-images.ps1", "-OutputDirectory", "artifacts/installer-images")
    runtime = resolve_runtime()
    file_version = version.split("-")[0]
    for language, language_name in LANGUAGES.items():
        run(
            f"Compile the {language_name} installer with {inno_compiler}",
            inno_compiler,
            "/Q",
            f"/DAppVersion={version}",
            f"/DFileVersion={file_version}",
            f"/DLang={language}",
            f"/DRuntimeVersion={runtime.version}",
            f"/DRuntimeUrl={runtime.url}",
            f"/DRuntimeSha256={runtime.sha256}",
            ROOT / "installer" / "Somnus.iss",
        )
        shutil.copy2(INSTALLER_DIR / f"Somnus-Setup-{version}-{language}.exe", RELEASE_DIR)


def zip_portable_folders(version: str) -> None:
    """Zip each portable folder with forward slashes, so every unzip tool reads the paths."""
    print("== Zip the portable builds", flush=True)
    for language in LANGUAGES:
        folder = portable_dir(language)
        archive = RELEASE_DIR / f"Somnus-{version}-win-x64-portable-{language}.zip"
        with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as zip_file:
            for path in build_paths(folder):
                zip_file.write(folder / path, path)


def write_checksums() -> None:
    """Write SHA256SUMS.txt for everything in the output folder and list the files."""
    shutil.copy2(NOTICES, RELEASE_DIR)
    lines = []
    for path in sorted(RELEASE_DIR.iterdir()):
        lines.append(f"{hashlib.sha256(path.read_bytes()).hexdigest()}  {path.name}\n")
        print(f"{path.name:<46} {path.stat().st_size / 1024:>10,.0f} KB", flush=True)
    (RELEASE_DIR / "SHA256SUMS.txt").write_text("".join(lines), encoding="ascii", newline="\n")


def back_up(version: str) -> None:
    """Keep a copy of this build, for milestones and on Mac's request (SWPA 3.15)."""
    reports = sorted(ARTIFACTS.glob("self-test-*.json"))
    run(
        "Keep a copy of the build",
        sys.executable, ROOT / "scripts" / "rotate_backups.py",
        "--add", RELEASE_DIR, BUILT_MANIFEST, *reports, "--version", version,
    )


def parse_arguments(arguments: list[str]) -> argparse.Namespace:
    """Read the command line."""
    parser = argparse.ArgumentParser(description="Builds the Somnus downloads from a clean state.")
    parser.add_argument("--version", default="0.0.0-local", help="the version, for example 1.1.0 or 1.1.0-rc.1")
    parser.add_argument("--skip-installers", action="store_true", help="stop at the portable builds (for pull requests)")
    parser.add_argument("--update-manifest", action="store_true", help="write docs/build-manifest-win-x64.txt instead of comparing it")
    parser.add_argument("--inno-compiler", help="the path of ISCC.exe")
    parser.add_argument("--backup", action="store_true", help="keep a copy of the finished build")
    options = parser.parse_args(arguments)
    if not VERSION_PATTERN.match(options.version):
        parser.error(f"{options.version} is not MAJOR.MINOR.PATCH[-suffix].")
    return options


def build(options: argparse.Namespace) -> None:
    """Run every step in order."""
    inno_compiler = None if options.skip_installers else find_inno_compiler(options.inno_compiler)
    clean()
    run(
        "Publish the framework-dependent win-x64 folder",
        "dotnet", "publish", "src/Somnus.App/Somnus.App.csproj",
        "--configuration", "Release", "--runtime", "win-x64", "--no-self-contained",
        f"-p:Version={options.version}", "--output", PUBLISH_DIR,
    )
    run_powershell("License notices", "check-licenses.ps1", "-OutputFile", "artifacts/THIRD_PARTY_LICENSES.txt")
    shutil.copy2(NOTICES, PUBLISH_DIR)
    write_package_texts(options.version)
    assemble_portable_folders()
    for language in LANGUAGES:
        run_self_test(language)
    check_contents(options.update_manifest)
    report_size()
    if inno_compiler is not None:
        compile_installers(options.version, inno_compiler)
    zip_portable_folders(options.version)
    write_checksums()
    if options.backup:
        back_up(options.version)


def main() -> int:
    """Build, and turn a failure into one readable line."""
    try:
        build(parse_arguments(sys.argv[1:]))
    except BuildError as error:
        print(f"Build failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
