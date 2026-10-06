"""Real ZIP fixtures exercise extraction boundaries, identity, and content."""
import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest
import zipfile

MODULE_PATH = Path(__file__).resolve().parents[2] / 'tools' / 'prepare_port.py'


class PrepareArchiveTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.prepare = None
        if MODULE_PATH.exists():
            spec = importlib.util.spec_from_file_location('prepare_port', MODULE_PATH)
            module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)
            self.prepare = module.prepare_archive

    def make_archive(self, entries):
        archive = self.root / 'game.zip'
        with zipfile.ZipFile(archive, 'w') as z:
            for name, content in entries:
                z.writestr(name, content)
        digest = hashlib.sha256(archive.read_bytes()).hexdigest()
        return archive, digest

    def test_rejects_path_traversal(self):
        self.assertIsNotNone(self.prepare, 'Archive preparation is not implemented')
        for name in ['../escape.txt', 'Stardew Valley/../../escape.txt',
                     'Stardew Valley/..\\escape.txt', '/tmp/escape.txt']:
            with self.subTest(name=name):
                archive, digest = self.make_archive([(name, b'outside')])
                with self.assertRaises(ValueError):
                    self.prepare(archive, self.root / 'out', expected_sha256=digest)
                self.assertFalse((self.root / 'escape.txt').exists())
                self.assertFalse((self.root / 'out').exists())

    def test_checks_archive_identity(self):
        self.assertIsNotNone(self.prepare, 'Archive preparation is not implemented')
        archive, _ = self.make_archive([('Stardew Valley/file.txt', b'wrong game')])
        with self.assertRaisesRegex(ValueError, 'SHA-256'):
            self.prepare(archive, self.root / 'out')
        self.assertFalse((self.root / 'out').exists())

    def test_extracts_required_assets(self):
        self.assertIsNotNone(self.prepare, 'Archive preparation is not implemented')
        archive, digest = self.make_archive([
            ('Stardew Valley/', b''),
            ('Stardew Valley/Stardew Valley.dll', b'game assembly'),
            ('Stardew Valley/StardewValley.GameData.dll', b'data assembly'),
            ('Stardew Valley/Content/Data/Crops.xnb', b'original crops'),
        ])
        output = self.root / 'out'
        report = self.prepare(archive, output, expected_sha256=digest)
        self.assertEqual(report['archive_sha256'], digest)
        self.assertEqual(report['files'], 3)
        self.assertEqual((output / 'Stardew Valley.dll').read_bytes(), b'game assembly')
        self.assertEqual((output / 'StardewValley.GameData.dll').read_bytes(), b'data assembly')
        self.assertEqual((output / 'Content/Data/Crops.xnb').read_bytes(), b'original crops')

    def test_does_not_overwrite_an_existing_extraction(self):
        self.assertIsNotNone(self.prepare, 'Archive preparation is not implemented')
        archive, digest = self.make_archive([('Stardew Valley/file.txt', b'new')])
        output = self.root / 'out'
        output.mkdir()
        (output / 'file.txt').write_bytes(b'previous')
        with self.assertRaises(FileExistsError):
            self.prepare(archive, output, expected_sha256=digest)
        self.assertEqual((output / 'file.txt').read_bytes(), b'previous')


if __name__ == '__main__':
    unittest.main()
