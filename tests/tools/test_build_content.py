"""Manifest tests detect renamed assets, altered bytes, and incomplete inputs."""
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

MODULE_PATH = Path(__file__).resolve().parents[2] / 'tools' / 'build_content.py'


class BuildContentTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.original = self.root / 'original'
        self.original.mkdir()
        self.build = None
        if MODULE_PATH.exists():
            spec = importlib.util.spec_from_file_location('build_content', MODULE_PATH)
            module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)
            self.build = module.build_manifest

    def fixture(self, entries):
        hashes = {}
        for name, data in entries.items():
            file = self.original / 'Content' / name
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(data)
            hashes['Content/' + name] = hashlib.sha256(data).hexdigest()
        (self.original / '.archive-manifest.json').write_text(json.dumps({
            'archive_sha256': 'fixture', 'file_sha256': hashes}))

    def test_preserves_original_names_hashes_and_load_groups(self):
        self.assertIsNotNone(self.build, 'Content manifest generation is not implemented')
        self.fixture({'Data/Crops.xnb': b'original data', 'Fonts/Japanese.fnt': b'original font',
                      'XACT/Wave Bank.xwb': b'original audio'})
        manifest = self.build(self.original, self.root / 'content.json', expected_sha256='fixture')
        entries = {entry['name']: entry for entry in manifest['assets']}
        self.assertEqual(set(entries), {'Data/Crops', 'Fonts/Japanese.fnt', 'XACT/Wave Bank.xwb'})
        self.assertEqual(entries['Data/Crops']['path'], 'Content/Data/Crops.xnb')
        self.assertEqual(entries['Data/Crops']['sha256'], hashlib.sha256(b'original data').hexdigest())
        self.assertEqual(entries['XACT/Wave Bank.xwb']['group'], 'audio-bank')
        self.assertEqual(entries['Data/Crops']['group'], 'content')
        self.assertEqual((self.original / 'Content/Data/Crops.xnb').read_bytes(), b'original data')

    def test_rejects_checksum_mismatch_before_writing_manifest(self):
        self.assertIsNotNone(self.build, 'Content manifest generation is not implemented')
        self.fixture({'Maps/Farm.xnb': b'original'})
        (self.original / 'Content/Maps/Farm.xnb').write_bytes(b'altered')
        with self.assertRaisesRegex(ValueError, 'Maps/Farm.xnb'):
            self.build(self.original, self.root / 'content.json', expected_sha256='fixture')
        self.assertFalse((self.root / 'content.json').exists())

    def test_reports_missing_original_asset(self):
        self.assertIsNotNone(self.build, 'Content manifest generation is not implemented')
        self.fixture({'Maps/Farm.xnb': b'original'})
        (self.original / 'Content/Maps/Farm.xnb').unlink()
        with self.assertRaisesRegex(FileNotFoundError, 'Maps/Farm.xnb'):
            self.build(self.original, self.root / 'content.json', expected_sha256='fixture')
        self.assertFalse((self.root / 'content.json').exists())

    def test_is_deterministic_and_rejects_case_collisions(self):
        self.assertIsNotNone(self.build, 'Content manifest generation is not implemented')
        self.fixture({'Maps/Farm.xnb': b'original'})
        self.build(self.original, self.root / 'one.json', expected_sha256='fixture')
        self.build(self.original, self.root / 'two.json', expected_sha256='fixture')
        self.assertEqual((self.root / 'one.json').read_bytes(), (self.root / 'two.json').read_bytes())
        self.fixture({'Maps/Farm.xnb': b'original', 'Maps/farm.xnb': b'collision'})
        with self.assertRaisesRegex(ValueError, 'collision'):
            self.build(self.original, self.root / 'collision.json', expected_sha256='fixture')
