#!/usr/bin/env python3
"""Read the supplied XACT46 bank without decoding or loading its audio data."""
import argparse
import hashlib
import json
from pathlib import Path
import struct


def inspect_bank(path: Path) -> dict:
    with path.open('rb') as stream:
        size = path.stat().st_size

        def read(count):
            data = stream.read(count)
            if len(data) != count:
                raise ValueError('Truncated wave bank metadata')
            return data

        if read(4) != b'WBND' or struct.unpack('<II', read(8)) != (46, 44):
            raise ValueError('Expected the supplied XACT46 wave bank format')
        segments = [struct.unpack('<II', read(8)) for _ in range(5)]
        if any(offset + length > size for offset, length in segments):
            raise ValueError('Wave bank segment range exceeds the file')
        stream.seek(segments[0][0])
        flags, count = struct.unpack('<II', read(8))
        name = read(64).split(b'\0')[0].decode('utf-8')
        metadata_size, name_size, alignment, compact_format = struct.unpack('<4I', read(16))
        build_time = struct.unpack('<Q', read(8))[0]
        if flags & 0x20000 or metadata_size != 24 or count * metadata_size != segments[1][1]:
            raise ValueError('Unexpected compact or variable-size wave metadata')
        stream.seek(segments[1][0])
        entries = []
        for index in range(count):
            duration_flags, format_bits, offset, length, loop_start, loop_length = struct.unpack('<6I', read(24))
            if offset + length > segments[4][1]:
                raise ValueError(f'Wave entry {index} range exceeds wave data')
            codec, channels, rate = format_bits & 3, (format_bits >> 2) & 7, (format_bits >> 5) & 0x3ffff
            format_alignment = (format_bits >> 23) & 255
            if codec not in (0, 2):
                raise ValueError(f'Unsupported wave codec {codec} at entry {index}')
            if channels not in (1, 2) or rate == 0:
                raise ValueError(f'Invalid channel/rate metadata at entry {index}')
            if codec == 0:
                bits = 16 if format_bits >> 31 else 8
                block_alignment = channels * bits // 8
                samples = length // block_alignment
            else:
                bits = 4
                block_alignment = (format_alignment + 22) * channels
                samples_per_block = (block_alignment // channels - 7) * 2 + 2
                blocks, remainder = divmod(length, block_alignment)
                if remainder and remainder < 7 * channels:
                    raise ValueError(f'Invalid partial ADPCM block at entry {index}')
                samples = blocks * samples_per_block + (2 + (remainder - 7 * channels) * 2 // channels if remainder else 0)
            if loop_start + loop_length > samples:
                raise ValueError(f'Loop range exceeds decoded samples at entry {index}')
            entries.append({'index': index, 'format': format_bits, 'flags': duration_flags & 15,
                            'codec': 'pcm' if codec == 0 else 'ms-adpcm', 'channels': channels, 'rate': rate,
                            'bits': bits, 'block_alignment': block_alignment, 'declared_samples': duration_flags >> 4,
                            'decoded_samples': samples, 'offset': segments[4][0] + offset, 'size': length,
                            'loop_start': loop_start, 'loop_length': loop_length})
        stream.seek(0)
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    return {'name': name, 'version': 46, 'header_version': 44, 'flags': flags, 'alignment': alignment,
            'entry_name_size': name_size, 'compact_format': compact_format, 'build_time': build_time,
            'size': size, 'sha256': digest, 'entries': entries}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('bank', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    result = inspect_bank(args.bank)
    args.output.write_text(json.dumps(result, indent=2) + '\n')
    print(f"Inspected {len(result['entries'])} entries in {result['name']}")
