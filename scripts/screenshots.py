#!/usr/bin/env python3
"""Renders the app window in named states into .audit/screenshots/ (SWPA 3.11).

The states come from tests/Somnus.App.Tests/ViewSmokeTests, which lays out every window off screen
through WPF. Nothing is shown, the screen is never captured and no power action runs, so this is safe
to run at any time. ViewSmokeTests names the states.

Usage:
    py -3 scripts/screenshots.py
"""

from __future__ import annotations

import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TEST_PROJECT = ROOT / "tests" / "Somnus.App.Tests" / "Somnus.App.Tests.csproj"
TEST_OUTPUT = ROOT / "tests" / "Somnus.App.Tests" / "bin" / "Release" / "net10.0-windows"
SCREENSHOTS = ROOT / ".audit" / "screenshots"


def main() -> int:
    """Run the smoke test and copy its pictures."""
    subprocess.run(["dotnet", "build", TEST_PROJECT, "-c", "Release", "--nologo", "-v", "q"], cwd=ROOT, check=True)
    subprocess.run(
        [TEST_OUTPUT / "Somnus.App.Tests.exe", "-class", "Somnus.App.Tests.ViewSmokeTests"],
        cwd=TEST_OUTPUT, check=True,
    )
    shutil.rmtree(SCREENSHOTS, ignore_errors=True)
    SCREENSHOTS.mkdir(parents=True)
    pictures = sorted((TEST_OUTPUT / "previews").glob("*.png"))
    for picture in pictures:
        shutil.copy2(picture, SCREENSHOTS)
    print(f"{len(pictures)} pictures in {SCREENSHOTS.relative_to(ROOT).as_posix()}", flush=True)
    return 0 if pictures else 1


if __name__ == "__main__":
    sys.exit(main())
