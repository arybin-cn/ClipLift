// Generates ClipLift.ico: the idle tray icon (white up arrow on a green circle) at several sizes.
// Pure managed rasterizer, so it runs anywhere the .NET SDK runs. Keep the geometry in sync with IconFactory.cs.
//
// Usage: dotnet run --project tools/IconGen -- ClipLift.ico

using System.IO.Compression;

internal static class Program
{
    private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
    private const int Samples = 8; // supersampling per axis

    private static int Main(string[] args)
    {
        string output = args.Length > 0 ? args[0] : "ClipLift.ico";
        var images = Sizes.Select(s => (Size: s, Png: EncodePng(s, Render(s)))).ToList();

        using var stream = File.Create(output);
        using var w = new BinaryWriter(stream);
        w.Write((ushort)0);
        w.Write((ushort)1);
        w.Write((ushort)images.Count);

        int offset = 6 + 16 * images.Count;
        foreach (var (size, png) in images)
        {
            w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((ushort)1);
            w.Write((ushort)32);
            w.Write(png.Length);
            w.Write(offset);
            offset += png.Length;
        }
        foreach (var (_, png) in images)
            w.Write(png);

        Console.WriteLine($"Wrote {output} ({offset} bytes)");
        return 0;
    }

    /// <summary>Returns straight (non-premultiplied) RGBA pixels.</summary>
    private static byte[] Render(int s)
    {
        // Same geometry as IconFactory.Draw / DrawArrow.
        float cx = s / 2f, tip = s * 0.27f, tail = s * 0.73f, wing = s * 0.18f;
        float half = Math.Max(1.5f, s * 0.09f) / 2f;
        var segments = new[]
        {
            (cx, tail, cx, tip),
            (cx - wing, tip + wing, cx, tip),
            (cx, tip, cx + wing, tip + wing),
        };
        (float R, float G, float B) green = (0x16, 0xA3, 0x4A), white = (255, 255, 255);
        float radius = s / 2f;

        var rgba = new byte[s * s * 4];
        for (int py = 0; py < s; py++)
        for (int px = 0; px < s; px++)
        {
            float r = 0, g = 0, b = 0, a = 0;
            for (int sy = 0; sy < Samples; sy++)
            for (int sx = 0; sx < Samples; sx++)
            {
                float x = px + (sx + 0.5f) / Samples;
                float y = py + (sy + 0.5f) / Samples;
                float dx = x - cx, dy = y - radius;
                if (dx * dx + dy * dy > radius * radius)
                    continue;

                var c = segments.Any(seg => DistanceToSegment(x, y, seg) <= half) ? white : green;
                r += c.R; g += c.G; b += c.B; a += 1;
            }

            int i = (py * s + px) * 4;
            if (a > 0)
            {
                rgba[i] = (byte)Math.Round(r / a);
                rgba[i + 1] = (byte)Math.Round(g / a);
                rgba[i + 2] = (byte)Math.Round(b / a);
                rgba[i + 3] = (byte)Math.Round(255 * a / (Samples * Samples));
            }
        }
        return rgba;
    }

    private static float DistanceToSegment(float x, float y, (float X1, float Y1, float X2, float Y2) seg)
    {
        float vx = seg.X2 - seg.X1, vy = seg.Y2 - seg.Y1;
        float t = Math.Clamp(((x - seg.X1) * vx + (y - seg.Y1) * vy) / (vx * vx + vy * vy), 0, 1);
        float dx = x - (seg.X1 + t * vx), dy = y - (seg.Y1 + t * vy);
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static byte[] EncodePng(int size, byte[] rgba)
    {
        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            for (int y = 0; y < size; y++)
            {
                z.WriteByte(0); // filter: none
                z.Write(rgba, y * size * 4, size * 4);
            }
        }

        using var png = new MemoryStream();
        png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var header = new byte[13];
        WriteBigEndian(header, 0, size);
        WriteBigEndian(header, 4, size);
        header[8] = 8; // bit depth
        header[9] = 6; // RGBA
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", raw.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var buf = new byte[4];
        WriteBigEndian(buf, 0, data.Length);
        s.Write(buf);
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        WriteBigEndian(buf, 0, (int)Crc32(typeBytes.Concat(data)));
        s.Write(buf);
    }

    private static void WriteBigEndian(byte[] buf, int offset, int value)
    {
        buf[offset] = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)value;
    }

    private static uint Crc32(IEnumerable<byte> bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in bytes)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return ~crc;
    }
}
