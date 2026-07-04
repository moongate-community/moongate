using System.Buffers.Binary;
using System.Text;
using Moongate.Ultima.Imaging;
using Moongate.Ultima.Io;

namespace Moongate.Ultima.Graphics;

public static class Hues
{
    private static int[] _header;

    public static Hue[] List { get; private set; }

    static Hues()
    {
        Initialize();
    }

    public static unsafe void ApplyTo(UltimaBitmap bmp, ushort[] colors, bool onlyHueGrayPixels)
    {
        var stride = bmp.Stride >> 1;
        var width = bmp.Width;
        var height = bmp.Height;
        var delta = stride - width;

        var pBuffer = (ushort*)bmp.Scan0;
        var pLineEnd = pBuffer + width;
        var pImageEnd = pBuffer + stride * height;

        if (onlyHueGrayPixels)
        {
            while (pBuffer < pImageEnd)
            {
                while (pBuffer < pLineEnd)
                {
                    int c = *pBuffer;

                    if (c != 0)
                    {
                        var r = (c >> 10) & 0x1F;
                        var g = (c >> 5) & 0x1F;
                        var b = c & 0x1F;

                        if (r == g && r == b)
                        {
                            *pBuffer = (ushort)(colors[(c >> 10) & 0x1F] | 0x8000);
                        }
                    }
                    ++pBuffer;
                }

                pBuffer += delta;
                pLineEnd += stride;
            }
        }
        else
        {
            while (pBuffer < pImageEnd)
            {
                while (pBuffer < pLineEnd)
                {
                    if (*pBuffer != 0)
                    {
                        *pBuffer = (ushort)(colors[(*pBuffer >> 10) & 0x1F] | 0x8000);
                    }

                    ++pBuffer;
                }

                pBuffer += delta;
                pLineEnd += stride;
            }
        }
    }

    /// <summary>
    /// Exports list of all hue names and id (as hex)
    /// </summary>
    /// <param name="fileName">Output file name</param>
    public static void ExportHueList(string fileName)
    {
        var sb = new StringBuilder(90_0000);

        foreach (var hue in List)
        {
            sb.Append("0x").AppendFormat("{0:X}", hue.Index).Append(' ').AppendLine(hue.Name);
        }

        File.WriteAllText(fileName, sb.ToString());
    }

    /// <summary>
    /// Returns <see cref="Hue" />
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public static Hue GetHue(int index)
    {
        index &= 0x3FFF;

        if (index >= 0 && index < 3000)
        {
            return List[index];
        }

        return List[0];
    }

    /// <summary>
    /// Reads hues.mul and fills <see cref="List" />
    /// </summary>
    public static void Initialize()
    {
        var path = Files.GetFilePath("hues.mul");
        var index = 0;

        const int maxHueCount = 3000;
        List = new Hue[maxHueCount];

        if (path != null)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var blockCount = (int)fs.Length / 708;

                if (blockCount > 375)
                {
                    blockCount = 375;
                }

                _header = new int[blockCount];

                // Disk layout per HueDataMul: 32 ushorts (64) + 2 ushorts (4) + 20-byte name = 88 bytes.
                // Each block = 4-byte header + 8 * 88 = 708 bytes.
                const int hueDataSize = 88;
                const int blockSize = 4 + 8 * hueDataSize;
                var buffer = new byte[blockCount * blockSize];
                fs.ReadExactly(buffer, 0, buffer.Length);
                ReadOnlySpan<byte> bufferSpan = buffer;

                var cursor = 0;

                for (var i = 0; i < blockCount; ++i)
                {
                    _header[i] = BinaryPrimitives.ReadInt32LittleEndian(bufferSpan.Slice(cursor));
                    cursor += 4;

                    for (var j = 0; j < 8; ++j, ++index)
                    {
                        List[index] = new(index, bufferSpan.Slice(cursor, hueDataSize));
                        cursor += hueDataSize;
                    }
                }
            }
        }

        for (; index < List.Length; ++index)
        {
            List[index] = new(index);
        }
    }

    public static void Save(string path)
    {
        var mul = Path.Combine(path, "hues.mul");

        using (var fsMul = new FileStream(mul, FileMode.Create, FileAccess.Write, FileShare.Write))
        {
            using (var binMul = new BinaryWriter(fsMul))
            {
                var index = 0;

                foreach (var blockIdx in _header)
                {
                    binMul.Write(blockIdx);

                    for (var j = 0; j < 8; ++j, ++index)
                    {
                        for (var colorIndex = 0; colorIndex < 32; ++colorIndex)
                        {
                            binMul.Write(List[index].Colors[colorIndex]);
                        }

                        binMul.Write(List[index].TableStart);
                        binMul.Write(List[index].TableEnd);

                        var nameBuffer = new byte[20];

                        if (List[index].Name != null)
                        {
                            var bytes = Encoding.ASCII.GetBytes(List[index].Name);

                            if (bytes.Length > 20)
                            {
                                Array.Resize(ref bytes, 20);
                            }

                            bytes.CopyTo(nameBuffer, 0);
                        }

                        binMul.Write(nameBuffer);
                    }
                }
            }
        }
    }
}
