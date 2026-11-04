#!/usr/bin/env python3
"""Keeps copies of Somnus builds (SWPA 3.15).

A copy is a folder RRRRMMDD-HHMM_vX.Y.Z_<sha> in the backup folder, with the zip of the build, the
installers, the manifest and the self-test reports. After every new copy the rotation keeps the first
copy and the ten newest; before it removes an older one, it adds a line to docs/BUILDS.md.

The backup folder is named only here, because the SWPA guard blocks every command that names it.
Agents reach the copies through this script and scripts/build.py --backup.

Usage:
    py -3 scripts/rotate_backups.py --add artifacts/release --version 1.1.0
    py -3 scripts/rotate_backups.py --add builds/1.0.0 --version 1.0.0 --commit v1.0.0 --date 2026-10-23T12:00
    py -3 scripts/rotate_backups.py --list
"""

from __future__ import annotations

import argparse
import json
import re
import shutil
import subprocess
import sys
import zipfile
from dataclasses import asdict, dataclass
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BACKUP_DIR = ROOT / "_build_backups"
BUILDS_LOG = ROOT / "docs" / "BUILDS.md"
RECORD_NAME = "backup.json"
MANIFEST_NAME = "build-manifest-win-x64.txt"
KEEP_NEWEST = 10
COPY_NAME = re.compile(r"^\d{8}-\d{4}_v.+_[0-9a-f]+$")
NO_REPORT = "brak raportu"


class BackupError(Exception):
    """A copy could not be made; the message says why."""


@dataclass(frozen=True)
class Record:
    """What docs/BUILDS.md keeps about a copy after the rotation removed it."""

    date: str
    version: str
    commit: str
    size_mb: float
    files: int
    self_test: str
    notes: str


def short_commit(reference: str) -> str:
    """Return the short hash of a git reference."""
    result = subprocess.run(
        ["git", "rev-parse", "--short=7", f"{reference}^{{commit}}"],
        cwd=ROOT, capture_output=True, text=True, check=False,
    )
    if result.returncode != 0:
        raise BackupError(f"Git does not know {reference}.")
    return result.stdout.strip()


def files_of(paths: list[Path]) -> list[Path]:
    """Expand folders into the files directly inside them."""
    files: list[Path] = []
    for path in paths:
        if path.is_dir():
            files += sorted(item for item in path.iterdir() if item.is_file())
        elif path.is_file():
            files.append(path)
        else:
            raise BackupError(f"{path} does not exist.")
    if not files:
        raise BackupError("There is nothing to copy.")
    return files


def portable_zip(copy: Path) -> Path | None:
    """Return the zip that holds the build, if the copy has one."""
    return next(iter(sorted(copy.glob("*portable*.zip"))), None)


def write_manifest_from_zip(copy: Path) -> None:
    """Older builds and CI downloads may come without a manifest; the zip knows the file list."""
    archive = portable_zip(copy)
    if archive is None or (copy / MANIFEST_NAME).exists():
        return
    with zipfile.ZipFile(archive) as zip_file:
        names = sorted(name.replace("\\", "/") for name in zip_file.namelist() if not name.endswith(("/", "\\")))
    (copy / MANIFEST_NAME).write_text("\n".join(names) + "\n", encoding="utf-8", newline="\n")


def measure(copy: Path) -> tuple[float, int]:
    """Return the size in MB and the file count of the build in a copy."""
    archive = portable_zip(copy)
    if archive is None:
        sizes = [path.stat().st_size for path in copy.iterdir() if path.is_file()]
    else:
        with zipfile.ZipFile(archive) as zip_file:
            sizes = [item.file_size for item in zip_file.infolist() if not item.is_dir()]
    return round(sum(sizes) / 1_000_000, 2), len(sizes)


def self_test_result(copy: Path) -> str:
    """Summarise the self-test reports of a copy."""
    reports = sorted(copy.glob("self-test-*.json"))
    if not reports:
        return NO_REPORT
    passed = all(json.loads(report.read_text(encoding="utf-8")).get("passed") is True for report in reports)
    return "zaliczony" if passed else "niezaliczony"


def add_copy(paths: list[Path], version: str, commit: str, when: datetime, notes: str) -> Path:
    """Copy build files into a new backup folder and record what they are."""
    copy = BACKUP_DIR / f"{when:%Y%m%d-%H%M}_v{version}_{commit}"
    if copy.exists():
        raise BackupError(f"The copy {copy.name} already exists.")
    sources = files_of(paths)
    copy.mkdir(parents=True)
    for source in sources:
        shutil.copy2(source, copy)
    write_manifest_from_zip(copy)
    size_mb, files = measure(copy)
    record = Record(f"{when:%Y-%m-%d}", version, commit, size_mb, files, self_test_result(copy), notes)
    (copy / RECORD_NAME).write_text(json.dumps(asdict(record), ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Copy {copy.name}: {size_mb} MB in {files} files.", flush=True)
    return copy


def copies() -> list[Path]:
    """Return the copies from the oldest to the newest; the date comes first in the name."""
    if not BACKUP_DIR.exists():
        return []
    return sorted(path for path in BACKUP_DIR.iterdir() if path.is_dir() and COPY_NAME.match(path.name))


def log_line(record: Record) -> str:
    """Format a row of the table in docs/BUILDS.md."""
    size = f"{record.size_mb} MB"
    return f"| {record.date} | {record.version} | {record.commit} | {size} | {record.files} | {record.self_test} | {record.notes} |\n"


def read_record(copy: Path) -> Record:
    """Read what a copy says about itself."""
    return Record(**json.loads((copy / RECORD_NAME).read_text(encoding="utf-8")))


def rotate() -> list[Path]:
    """Keep the first copy and the newest ones; describe the others in docs/BUILDS.md and remove them."""
    existing = copies()
    keep = {*existing[:1], *existing[-KEEP_NEWEST:]}
    removed = [copy for copy in existing if copy not in keep]
    for copy in removed:
        with BUILDS_LOG.open("a", encoding="utf-8", newline="\n") as log:
            log.write(log_line(read_record(copy)))
        shutil.rmtree(copy)
        print(f"Removed {copy.name} (described in docs/BUILDS.md).", flush=True)
    return removed


def list_copies() -> None:
    """Print the copies, because the guard blocks listing the folder directly."""
    for copy in copies():
        record = read_record(copy)
        print(f"{copy.name}  {record.size_mb} MB  {record.files} files  self-test: {record.self_test}", flush=True)


def parse_arguments(arguments: list[str]) -> argparse.Namespace:
    """Read the command line."""
    parser = argparse.ArgumentParser(description="Keeps copies of Somnus builds (SWPA 3.15).")
    parser.add_argument("--add", nargs="+", type=Path, metavar="PATH", help="files or folders to keep as a new copy")
    parser.add_argument("--version", help="the version of the build; needed with --add")
    parser.add_argument("--commit", default="HEAD", help="the git reference of the build")
    parser.add_argument("--date", type=datetime.fromisoformat, help="when the build was made, if not now")
    parser.add_argument("--note", default="", help="a remark for docs/BUILDS.md")
    parser.add_argument("--list", action="store_true", help="print the copies")
    options = parser.parse_args(arguments)
    if options.add and not options.version:
        parser.error("--add needs --version.")
    return options


def main() -> int:
    """Add a copy if asked, rotate, and turn a failure into one readable line."""
    options = parse_arguments(sys.argv[1:])
    try:
        if options.add:
            add_copy(options.add, options.version, short_commit(options.commit), options.date or datetime.now(), options.note)
        rotate()
        if options.list:
            list_copies()
    except BackupError as error:
        print(f"Backup failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
