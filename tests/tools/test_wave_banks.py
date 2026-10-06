"""Literal bank fixtures protect byte ranges, codecs, timing and loop metadata."""
import importlib.util
from pathlib import Path
import struct
import tempfile
import unittest

MODULE_PATH = Path(__file__).resolve().parents[2] / 'tools' / 'wave_banks.py'


class WaveBankTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.inspect = None
        if MODULE_PATH.exists():
            spec = importlib.util.spec_from_file_location('wave_banks', MODULE_PATH)
            module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)
            self.inspect = module.inspect_bank

    def bank(self, codec=0, data=None, samples=4, loop_start=1, loop_length=2):
        if data is None:
            data = struct.pack('<4h', -1000, 0, 1000, 0)
        alignment = 2 if codec == 0 else 0
        format_bits = codec | (1 << 2) | (22050 << 5) | (alignment << 23) | (1 << 31)
        segments = [(52, 96), (148, 24), (172, 0), (0, 0), (172, len(data))]
        header = b'WBND' + struct.pack('<II', 46, 44) + b''.join(struct.pack('<II', *segment) for segment in segments)
        bank = struct.pack('<II', 0, 1) + b'Fixture Bank'.ljust(64, b'\0') + struct.pack('<4IQ', 24, 0, 4, 0, 0)
        entry = struct.pack('<6I', samples << 4, format_bits, 0, len(data), loop_start, loop_length)
        file = self.root / 'bank.xwb'
        file.write_bytes(header + bank + entry + data)
        return file

    def test_keeps_pcm_byte_range_rate_channels_and_loop_points(self):
        self.assertIsNotNone(self.inspect, 'Wave bank inspection is not implemented')
        result = self.inspect(self.bank())
        self.assertEqual(result['name'], 'Fixture Bank')
        entry = result['entries'][0]
        self.assertEqual(entry['codec'], 'pcm')
        self.assertEqual((entry['offset'], entry['size']), (172, 8))
        self.assertEqual((entry['rate'], entry['channels'], entry['bits']), (22050, 1, 16))
        self.assertEqual((entry['decoded_samples'], entry['loop_start'], entry['loop_length']), (4, 1, 2))

    def test_decodes_adpcm_block_alignment_and_native_sample_count(self):
        self.assertIsNotNone(self.inspect, 'Wave bank inspection is not implemented')
        file = self.bank(codec=2, data=bytes(22), samples=31, loop_start=4, loop_length=8)
        entry = self.inspect(file)['entries'][0]
        self.assertEqual(entry['codec'], 'ms-adpcm')
        self.assertEqual(entry['block_alignment'], 22)
        self.assertEqual(entry['declared_samples'], 31)
        self.assertEqual(entry['decoded_samples'], 32)  # native decoder retains block padding
        self.assertEqual((entry['loop_start'], entry['loop_length']), (4, 8))

    def test_rejects_entry_ranges_outside_wave_data(self):
        self.assertIsNotNone(self.inspect, 'Wave bank inspection is not implemented')
        file = self.bank()
        damaged = bytearray(file.read_bytes())
        struct.pack_into('<I', damaged, 148 + 12, 1000)
        file.write_bytes(damaged)
        with self.assertRaisesRegex(ValueError, 'range'):
            self.inspect(file)

    def test_rejects_codecs_that_cannot_preserve_this_input(self):
        self.assertIsNotNone(self.inspect, 'Wave bank inspection is not implemented')
        with self.assertRaisesRegex(ValueError, 'codec'):
            self.inspect(self.bank(codec=1))
