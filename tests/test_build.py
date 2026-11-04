"""Tests of the logic in scripts/build.py; the build itself runs in CI (SWPA 3.14)."""

from __future__ import annotations

import importlib.util
import sys
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "build.py"
SPEC = importlib.util.spec_from_file_location("build", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
build = importlib.util.module_from_spec(SPEC)
sys.modules["build"] = build
SPEC.loader.exec_module(build)


class ForbiddenPathsTests(unittest.TestCase):
    def test_allows_what_the_app_needs(self) -> None:
        paths = [
            "LICENSE.txt",
            "README.txt",
            "Somnus.deps.json",
            "Somnus.dll",
            "Somnus.exe",
            "Somnus.ini",
            "Somnus.runtimeconfig.json",
            "THIRD_PARTY_LICENSES.txt",
            "pl/Somnus.Presentation.resources.dll",
        ]
        self.assertEqual(build.forbidden_paths(paths), [])

    def test_rejects_developer_content(self) -> None:
        paths = [
            "Somnus.pdb",
            "Somnus.xml",
            "Somnus.Core.Tests.dll",
            "docs/SCOPE.en.md",
            "scripts/build.py",
            "tests/data.json",
            ".env.local",
            ".git/config",
            ".pytest_cache/state.json",
            "xunit.core.dll",
            "testhost.exe",
            "Microsoft.TestPlatform.Utilities.dll",
            "zzz.mp3",
        ]
        self.assertEqual(build.forbidden_paths(paths), paths)


class PackageTextTests(unittest.TestCase):
    def test_uses_windows_line_endings_and_fills_in_the_version(self) -> None:
        self.assertEqual(build.package_text(["Somnus {version}\nline\n"], "1.2.3"), "Somnus 1.2.3\r\nline\r\n")

    def test_separates_parts_with_a_line(self) -> None:
        separator = "-" * 72
        self.assertEqual(build.package_text(["one\r\n", "two"], "1.0.0"), f"one\r\n\r\n{separator}\r\n\r\ntwo\r\n")


class BudgetTests(unittest.TestCase):
    def test_reads_the_budget_from_the_plan(self) -> None:
        self.assertEqual(build.parse_budget("## 11. Build budget\n\nBudget: 2.3 MB, 20 files (1.1.0).\n"), (2.3, 20))

    def test_a_plan_without_a_budget_gives_nothing(self) -> None:
        self.assertIsNone(build.parse_budget("## 11. Build budget\n"))

    def test_up_to_ten_percent_of_growth_is_within_the_budget(self) -> None:
        self.assertFalse(build.over_budget(2.2, 22, (2.0, 20)))

    def test_more_size_or_more_files_is_over_the_budget(self) -> None:
        self.assertTrue(build.over_budget(2.3, 20, (2.0, 20)))
        self.assertTrue(build.over_budget(2.0, 23, (2.0, 20)))


class ArgumentTests(unittest.TestCase):
    def test_takes_a_release_candidate_version(self) -> None:
        self.assertEqual(build.parse_arguments(["--version", "1.1.0-rc.5"]).version, "1.1.0-rc.5")

    def test_rejects_a_version_that_is_not_semver(self) -> None:
        with self.assertRaises(SystemExit):
            build.parse_arguments(["--version", "one"])


if __name__ == "__main__":
    unittest.main()
