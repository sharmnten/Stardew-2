#!/usr/bin/env python3
"""Read the supplied XACT46 bank without decoding or loading its audio data."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import os
import subprocess
import tempfile


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


def wrap_wave(entry: dict, data: bytes) -> bytes:
    """Add a WAV container; keep encoded samples and decoded padding unchanged."""
    if len(data) != entry['size']:
        raise ValueError('Wave entry byte length does not match its metadata')

    def chunk(name, payload):
        return name + struct.pack('<I', len(payload)) + payload + (b'\0' if len(payload) & 1 else b'')

    channels, rate, alignment = entry['channels'], entry['rate'], entry['block_alignment']
    if entry['codec'] == 'pcm':
        fmt = struct.pack('<HHIIHH', 1, channels, rate, rate * alignment, alignment, entry['bits'])
        body = b'WAVE' + chunk(b'fmt ', fmt) + chunk(b'data', data)
    elif entry['codec'] == 'ms-adpcm':
        samples_per_block = (alignment // channels - 7) * 2 + 2
        coefficients = [(256, 0), (512, -256), (0, 0), (192, 64), (240, 0), (460, -208), (392, -232)]
        extra = struct.pack('<HH', samples_per_block, len(coefficients)) + b''.join(struct.pack('<hh', *pair) for pair in coefficients)
        fmt = struct.pack('<HHIIHHH', 2, channels, rate, rate * alignment // samples_per_block, alignment, 4, len(extra)) + extra
        body = b'WAVE' + chunk(b'fmt ', fmt) + chunk(b'fact', struct.pack('<I', entry['decoded_samples'])) + chunk(b'data', data)
    else:
        raise ValueError('Unsupported wave codec: ' + entry['codec'])
    return b'RIFF' + struct.pack('<I', len(body)) + body


def encode_flac(entry: dict, data: bytes, output: Path) -> dict:
    """Encode losslessly and validate native timing before replacing output."""
    wave = wrap_wave(entry, data)
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=output.parent, suffix='.flac', delete=False) as temporary:
        staging = Path(temporary.name)
    try:
        subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-nostdin', '-i', 'pipe:0',
                        '-map_metadata', '-1', '-c:a', 'flac', '-sample_fmt', 's16', '-compression_level', '8',
                        '-fflags', '+bitexact', '-flags:a', '+bitexact', '-f', 'flac', '-y', str(staging)],
                       input=wave, capture_output=True, check=True)
        with staging.open('rb') as stream:
            header = stream.read(42)
            if len(header) != 42 or header[:4] != b'fLaC' or header[4] & 0x7f != 0 or header[5:8] != b'\0\0\x22':
                raise ValueError('Encoded FLAC has no valid STREAMINFO header')
            packed = int.from_bytes(header[18:26], 'big')
            rate, channels, bits, samples = packed >> 44, ((packed >> 41) & 7) + 1, ((packed >> 36) & 31) + 1, packed & 0xfffffffff
            if (rate, channels, bits, samples) != (entry['rate'], entry['channels'], 16, entry['decoded_samples']):
                raise ValueError(f'Lossless audio timing/format mismatch: {(rate, channels, bits, samples)}')
            stream.seek(0)
            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        size = staging.stat().st_size
        os.replace(staging, output)
        return {'sha256': digest, 'size': size, 'rate': rate, 'channels': channels, 'bits': bits, 'samples': samples,
                'pcm_md5': header[26:42].hex()}
    finally:
        if staging.exists():
            staging.unlink()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('bank', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    result = inspect_bank(args.bank)
    args.output.write_text(json.dumps(result, indent=2) + '\n')
    print(f"Inspected {len(result['entries'])} entries in {result['name']}")
