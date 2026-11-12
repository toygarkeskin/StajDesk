using System.Buffers.Binary;
using System.IO.Compression;

namespace StajDesk.Infrastructure.Persistence;

/// <summary>
/// Demo banner görselleri: iki renkli yatay geçişli (gradient) PNG üretir. Harici görsel indirmeye veya
/// telifli görsel kullanmaya gerek kalmaz; başlık ve açıklama arayüzde görselin üzerine yazılır.
/// </summary>
internal static class DemoImages
{
    public static byte[] Gradient(int width, int height, (byte R, byte G, byte B) from, (byte R, byte G, byte B) to)
    {
        // Ham görüntü: her satır başında filtre baytı (0) + RGB pikseller
        var raw = new byte[height * (1 + width * 3)];
        for (var y = 0; y < height; y++)
        {
            var row = y * (1 + width * 3);
            raw[row] = 0;
            for (var x = 0; x < width; x++)
            {
                // Hafif çapraz geçiş: sol üstten sağ alta
                var t = (x / (double)(width - 1)) * 0.8 + (y / (double)(height - 1)) * 0.2;
                var i = row + 1 + x * 3;
                raw[i] = (byte)(from.R + (to.R - from.R) * t);
                raw[i + 1] = (byte)(from.G + (to.G - from.G) * t);
                raw[i + 2] = (byte)(from.B + (to.B - from.B) * t);
            }
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8;  // bit derinliği
        ihdr[9] = 2;  // RGB
        Chunk(png, "IHDR", ihdr);
        using (var z = new MemoryStream())
        {
            using (var zlib = new ZLibStream(z, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(raw);
            Chunk(png, "IDAT", z.ToArray());
        }
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        var crc = Crc32(typeBytes, data);
        Span<byte> c = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(c, crc);
        s.Write(c);
    }

    private static readonly uint[] Table = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    private static uint Crc32(byte[] type, byte[] data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in type) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        foreach (var b in data) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
