"""Tests of scripts/rotate_backups.py (SWPA 3.15), in a temporary folder."""

from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
import unittest
import zipfile
from datetime import datetime, timedelta
from pathlib import Path

SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "rotate_backups.py"
SPEC = importlib.util.spec_from_file_location("rotate_backups", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
rotate_backups = importlib.util.module_from_spec(SPEC)
sys.modules["rotate_backups"] = rotate_backups
SPEC.loader.exec_module(rotate_backups)

LOG_HEADER = "| Data | Wersja |\n|---|---|\n"


class BackupTestCase(unittest.TestCase):
    def setUp(self) -> None:
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.sources = self.root / "sources"
        self.sources.mkdir()
        for name, value in (("BACKUP_DIR", self.root / "copies"), ("BUILDS_LOG", self.root / "BUILDS.md")):
            original = getattr(rotate_backups, name)
            setattr(rotate_backups, name, value)
            self.addCleanup(setattr, rotate_backups, name, original)
        rotate_backups.BUILDS_LOG.write_text(LOG_HEADER, encoding="utf-8")

    def make_build(self) -> Path:
        with zipfile.ZipFile(self.sources / "Somnus-1.0.0-win-x64-portable-en.zip", "w") as zip_file:
            zip_file.writestr("Somnus.exe", b"12345")
            zip_file.writestr("pl\\Somnus.Presentation.resources.dll", b"123")
        (self.sources / "Somnus-Setup-1.0.0-en.exe").write_bytes(b"setup")
        return self.sources

    def add(self, index: int) -> Path:
        when = datetime(2026, 10, 1, 12, 0) + timedelta(days=index)
        return rotate_backups.add_copy([self.make_build()], f"1.0.{index}", "abc1234", when, "")


class AddCopyTests(BackupTestCase):
    def test_names_the_folder_after_the_date_version_and_commit(self) -> None:
        copy = rotate_backups.add_copy([self.make_build()], "1.1.0-rc.5", "abc1234", datetime(2026, 10, 30, 9, 5), "")
        self.assertEqual(copy.name, "20261030-0905_v1.1.0-rc.5_abc1234")
        self.assertTrue((copy / "Somnus-Setup-1.0.0-en.exe").exists())

    def test_records_the_build_from_its_zip(self) -> None:
        copy = self.add(1)
        record = rotate_backups.read_record(copy)
        self.assertEqual((record.version, record.commit, record.files, record.self_test), ("1.0.1", "abc1234", 2, rotate_backups.NO_REPORT))
        self.assertEqual(record.date, "2026-10-02")

    def test_writes_the_manifest_from_the_zip_when_there_is_none(self) -> None:
        copy = self.add(1)
        manifest = (copy / rotate_backups.MANIFEST_NAME).read_text(encoding="utf-8")
        self.assertEqual(manifest, "Somnus.exe\npl/Somnus.Presentation.resources.dll\n")

    def test_summarises_the_self_test_reports(self) -> None:
        self.make_build()
        (self.sources / "self-test-en.json").write_text(json.dumps({"passed": True}), encoding="utf-8")
        (self.sources / "self-test-pl.json").write_text(json.dumps({"passed": False}), encoding="utf-8")
        copy = rotate_backups.add_copy([self.sources], "1.0.0", "abc1234", datetime(2026, 10, 30), "")
        self.assertEqual(rotate_backups.read_record(copy).self_test, "niezaliczony")

    def test_refuses_a_missing_source(self) -> None:
        with self.assertRaises(rotate_backups.BackupError):
            rotate_backups.add_copy([self.root / "nothing"], "1.0.0", "abc1234", datetime(2026, 10, 30), "")


class RotationTests(BackupTestCase):
    def test_keeps_everything_up_to_the_first_and_ten_newest(self) -> None:
        for index in range(11):
            self.add(index)
        self.assertEqual(rotate_backups.rotate(), [])
        self.assertEqual(len(rotate_backups.copies()), 11)

    def test_keeps_the_first_copy_and_the_ten_newest(self) -> None:
        added = [self.add(index) for index in range(13)]

        removed = rotate_backups.rotate()

        self.assertEqual(removed, added[1:3])
        self.assertEqual(rotate_backups.copies(), [added[0], *added[3:]])

    def test_describes_a_removed_copy_in_the_log(self) -> None:
        for index in range(12):
            self.add(index)

        rotate_backups.rotate()

        log = rotate_backups.BUILDS_LOG.read_text(encoding="utf-8")
        self.assertEqual(log, LOG_HEADER + "| 2026-10-02 | 1.0.1 | abc1234 | 0.0 MB | 2 | brak raportu |  |\n")


if __name__ == "__main__":
    unittest.main()
