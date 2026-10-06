using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace StardewBrowser.Platform.Audio.Xact;

internal static class StreamWave
{
    private static int nextTrack;
    internal static BrowserWave Read(Stream stream, bool vorbis)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        byte[] data = buffer.ToArray();
        var (rate, channels, samples) = vorbis ? ReadVorbis(data) : ReadWave(data);
        if (rate <= 0 || channels is < 1 or > 2 || samples <= 0) throw new InvalidDataException("Invalid audio stream dimensions.");
        int track = Interlocked.Increment(ref nextTrack);
        return new(-1, track, $"stream/{track}", Convert.ToHexString(SHA256.HashData(data)), data.Length,
            rate, channels, samples, new(0, 0), "stream", data);
    }

    private static (int Rate, int Channels, int Samples) ReadWave(byte[] data)
    {
        using var reader = new BinaryReader(new MemoryStream(data), Encoding.ASCII);
        if (new string(reader.ReadChars(4)) != "RIFF" || reader.ReadUInt32() != data.Length - 8 || new string(reader.ReadChars(4)) != "WAVE")
            throw new InvalidDataException("Invalid RIFF wave.");
        int rate = 0, channels = 0, blockAlign = 0, dataLength = 0, fact = 0;
        ushort format = 0;
        while (reader.BaseStream.Position < data.Length)
        {
            string id = new(reader.ReadChars(4));
            int length = checked((int)reader.ReadUInt32());
            long start = reader.BaseStream.Position;
            if (length > data.Length - start) throw new InvalidDataException("Truncated wave chunk.");
            if (id == "fmt ")
            {
                if (length < 16) throw new InvalidDataException("Truncated wave format.");
                format = reader.ReadUInt16();
                channels = reader.ReadUInt16(); rate = checked((int)reader.ReadUInt32());
                _ = reader.ReadUInt32(); blockAlign = reader.ReadUInt16(); _ = reader.ReadUInt16();
            }
            else if (id == "data") dataLength = length;
            else if (id == "fact" && length >= 4) fact = checked((int)reader.ReadUInt32());
            reader.BaseStream.Position = start + length + (length & 1);
        }
        if (blockAlign == 0 || format is not (1 or 2 or 3)) throw new NotSupportedException("Unsupported wave encoding.");
        return (rate, channels, format == 2 ? fact : dataLength / blockAlign);
    }

    private static (int Rate, int Channels, int Samples) ReadVorbis(byte[] data)
    {
        int offset = 0, rate = 0, channels = 0;
        long samples = 0;
        uint? serial = null;
        while (offset < data.Length)
        {
            var page = data.AsSpan(offset);
            if (page.Length < 27 || !page[..4].SequenceEqual("OggS"u8) || page[4] != 0) throw new InvalidDataException("Invalid Vorbis page.");
            uint pageSerial = BinaryPrimitives.ReadUInt32LittleEndian(page[14..]);
            serial ??= pageSerial;
            if (pageSerial != serial) throw new NotSupportedException("Chained Vorbis streams require a separate decoder.");
            int segments = page[26], bodyOffset = 27 + segments;
            if (page.Length < bodyOffset) throw new InvalidDataException("Truncated Vorbis segment table.");
            int bodyLength = 0;
            foreach (byte value in page.Slice(27, segments)) bodyLength += value;
            if (page.Length < bodyOffset + bodyLength) throw new InvalidDataException("Truncated Vorbis packet.");
            if (offset == 0)
            {
                var body = page.Slice(bodyOffset, bodyLength);
                if (body.Length < 30 || !body[..7].SequenceEqual(new byte[] { 1, 118, 111, 114, 98, 105, 115 }))
                    throw new InvalidDataException("Missing Vorbis identification packet.");
                channels = body[11]; rate = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(body[12..]));
            }
            long granule = BinaryPrimitives.ReadInt64LittleEndian(page[6..]);
            if (granule >= 0) samples = Math.Max(samples, granule);
            offset += bodyOffset + bodyLength;
        }
        return (rate, channels, checked((int)samples));
    }
}
