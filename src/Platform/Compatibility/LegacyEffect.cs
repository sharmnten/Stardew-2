using System.Text;

namespace StardewBrowser.Platform.Compatibility;

/// <summary>Adapts the original MGFX9 container to KNI's MGFX10 reader.</summary>
public static class LegacyEffect
{
    private const int Signature = 0x5846474d;

    public static byte[] Convert(byte[] original)
    {
        using var source = new MemoryStream(original, writable: false);
        using var reader = new BinaryReader(source, Encoding.UTF8);
        using var result = new MemoryStream();
        using var writer = new BinaryWriter(result, Encoding.UTF8, leaveOpen: true);
        if (reader.ReadInt32() != Signature || reader.ReadByte() != 9 || reader.ReadByte() != 0)
            throw new NotSupportedException("Expected an original MGFX9 OpenGL effect.");
        writer.Write(Signature);
        writer.Write((byte)10);
        writer.Write((byte)0);
        writer.Write(reader.ReadInt32()); // retain the effect-cache identity

        int buffers = reader.ReadByte();
        writer.Write(buffers);
        for (int i = 0; i < buffers; i++)
        {
            writer.Write(reader.ReadString());
            writer.Write(reader.ReadInt16());
            int bindings = reader.ReadByte();
            writer.Write(bindings);
            for (int j = 0; j < bindings; j++)
            {
                writer.Write((int)reader.ReadByte());
                writer.Write(reader.ReadUInt16());
            }
        }

        int shaders = reader.ReadByte();
        writer.Write(shaders);
        for (int i = 0; i < shaders; i++)
        {
            writer.Write(reader.ReadByte());
            int length = reader.ReadInt32();
            writer.Write(length);
            Copy(reader, writer, length); // preserve the actual original GLSL
            int samplers = reader.ReadByte();
            writer.Write((byte)samplers);
            for (int j = 0; j < samplers; j++)
            {
                Copy(reader, writer, 3); // type, texture slot, sampler slot
                bool hasState = reader.ReadBoolean();
                writer.Write(hasState);
                if (hasState) Copy(reader, writer, 20);
                writer.Write(reader.ReadString());
                writer.Write(reader.ReadByte());
            }
            int bindings = reader.ReadByte();
            writer.Write((byte)bindings);
            Copy(reader, writer, bindings);
            int attributes = reader.ReadByte();
            writer.Write((byte)attributes);
            for (int j = 0; j < attributes; j++)
            {
                writer.Write(reader.ReadString());
                Copy(reader, writer, 4);
            }
        }
        ReadParameters(reader, writer, 0);
        int techniques = reader.ReadByte();
        writer.Write(techniques);
        for (int i = 0; i < techniques; i++)
        {
            writer.Write(reader.ReadString());
            ReadAnnotations(reader, writer);
            int passes = reader.ReadByte();
            writer.Write(passes);
            for (int j = 0; j < passes; j++)
            {
                writer.Write(reader.ReadString());
                ReadAnnotations(reader, writer);
                int vertex = reader.ReadByte(), pixel = reader.ReadByte();
                writer.Write(vertex == 255 ? -1 : vertex);
                writer.Write(pixel == 255 ? -1 : pixel);
                foreach (int stateBytes in new[] { 18, 25, 12 })
                {
                    bool present = reader.ReadBoolean();
                    writer.Write(present);
                    if (present) Copy(reader, writer, stateBytes);
                }
            }
        }
        if (source.Position != source.Length) throw new InvalidDataException("Unexpected trailing MGFX9 data.");
        writer.Write(Signature); // MGFX10 requires a validation tail
        return result.ToArray();
    }

    private static int ReadParameters(BinaryReader reader, BinaryWriter writer, int depth)
    {
        if (depth > 32) throw new InvalidDataException("Effect parameter nesting exceeds supported bounds.");
        int count = reader.Read7BitEncodedInt();
        if (count < 0 || count > 4096) throw new InvalidDataException("Invalid effect parameter count.");
        writer.Write(count);
        for (int i = 0; i < count; i++)
        {
            writer.Write(reader.ReadByte());
            byte type = reader.ReadByte();
            writer.Write(type);
            writer.Write(reader.ReadString());
            writer.Write(reader.ReadString());
            ReadAnnotations(reader, writer);
            byte rows = reader.ReadByte(), columns = reader.ReadByte();
            writer.Write(rows);
            writer.Write(columns);
            int elements = ReadParameters(reader, writer, depth + 1);
            int members = ReadParameters(reader, writer, depth + 1);
            if (elements != 0 || members != 0) continue;
            if (type is 1 or 2)
            {
                for (int j = 0; j < rows * columns; j++)
                {
                    float value = reader.ReadSingle();
                    writer.Write(type == 1 ? (value != 0 ? 1 : 0) : checked((int)value));
                }
            }
            else if (type == 3) Copy(reader, writer, rows * columns * 4);
            else if (type == 4) throw new NotSupportedException("String effect parameters are unsupported by the original and KNI readers.");
        }
        return count;
    }

    private static void ReadAnnotations(BinaryReader reader, BinaryWriter writer)
    {
        int count = reader.ReadByte();
        if (count != 0) throw new NotSupportedException("Nonempty effect annotations require a dedicated adapter.");
        writer.Write(count);
    }

    private static void Copy(BinaryReader reader, BinaryWriter writer, int length)
    {
        if (length < 0 || length > reader.BaseStream.Length - reader.BaseStream.Position) throw new EndOfStreamException("Truncated effect data.");
        writer.Write(reader.ReadBytes(length));
    }

    public static byte[] ConvertXnb(byte[] original)
    {
        using var source = new MemoryStream(original, writable: false);
        using var reader = new BinaryReader(source, Encoding.UTF8);
        if (Encoding.ASCII.GetString(reader.ReadBytes(3)) != "XNB") throw new InvalidDataException("Expected XNB effect content.");
        _ = reader.ReadByte();
        if (reader.ReadByte() != 5 || (reader.ReadByte() & 0xc0) != 0) throw new NotSupportedException("Expected an uncompressed XNB5 effect.");
        if (reader.ReadInt32() != original.Length) throw new InvalidDataException("XNB effect length does not match its header.");
        if (reader.Read7BitEncodedInt() != 1 || !reader.ReadString().StartsWith("Microsoft.Xna.Framework.Content.EffectReader,"))
            throw new NotSupportedException("Expected an original effect content reader.");
        if (reader.ReadInt32() != 0 || reader.Read7BitEncodedInt() != 0 || reader.Read7BitEncodedInt() != 1)
            throw new NotSupportedException("Unsupported effect resource layout.");
        int prefixLength = (int)source.Position;
        int effectLength = reader.ReadInt32();
        if (effectLength < 0 || effectLength != source.Length - source.Position) throw new InvalidDataException("Invalid XNB effect payload size.");
        byte[] effect = Convert(reader.ReadBytes(effectLength));
        using var result = new MemoryStream();
        using var writer = new BinaryWriter(result);
        writer.Write(original.AsSpan(0, prefixLength));
        writer.Write(effect.Length);
        writer.Write(effect);
        result.Position = 6;
        writer.Write((int)result.Length);
        return result.ToArray();
    }
}
