using System.Buffers.Binary;
using Moongate.UO.Data.Files;
using Moongate.UO.Data.Interfaces.Art;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Moongate.UO.Data.Services.Art;

/// <summary>
/// Default implementation for decoding item art images from UO art files.
/// </summary>
public sealed class ArtService : IArtService
{
    private readonly FileIndex _fileIndex;
    private readonly Dictionary<int, Image<Rgba32>> _itemCache = [];

    private byte[]? _streamBuffer;

    public ArtService()
        : this(
            new FileIndex(
                "Artidx.mul",
                "Art.mul",
                "artLegacyMUL.uop",
                0x14000,
                4,
                ".tga",
                0x13FDC,
                false
            )
        )
    {
    }

    public ArtService(FileIndex fileIndex)
        => _fileIndex = fileIndex;

    public Image<Rgba32>? GetArt(int itemId, bool clone = true)
    {
        var legalItemId = GetLegalItemId(itemId);

        if (legalItemId < 0)
        {
            return null;
        }

        if (_itemCache.TryGetValue(legalItemId, out var cachedImage))
        {
            return clone ? cachedImage.Clone() : cachedImage;
        }

        var staticIndex = legalItemId + 0x4000;
        var stream = _fileIndex.Seek(staticIndex, out var length, out _, out _);

        if (stream is null || length <= 0)
        {
            return null;
        }

        var image = LoadStatic(stream, length);

        if (image is null)
        {
            return null;
        }

        _itemCache[legalItemId] = image;

        return clone ? image.Clone() : image;
    }

    public bool IsValidArt(int itemId)
        => GetArt(itemId) is not null;

    private int GetLegalItemId(int itemId)
    {
        if (itemId < 0)
        {
            return -1;
        }

        return itemId > GetMaxItemId() ? -1 : itemId;
    }

    private int GetMaxItemId()
    {
        var idxLength = (int)(_fileIndex.IdxLength / 12);

        if (idxLength >= 0x13FDC)
        {
            return 0xFFDC;
        }

        if (idxLength == 0xC000)
        {
            return 0x7FFF;
        }

        return 0x3FFF;
    }

    private Image<Rgba32>? LoadStatic(Stream stream, int length)
    {
        if (_streamBuffer is null || _streamBuffer.Length < length)
        {
            _streamBuffer = new byte[length];
        }

        stream.ReadExactly(_streamBuffer, 0, length);

        if ((length & 1) != 0)
        {
            return null;
        }

        var source = _streamBuffer.AsSpan(0, length);

        if (source.Length < 8)
        {
            return null;
        }

        var width = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(4, 2));
        var height = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(6, 2));

        if (width == 0 || height == 0)
        {
            return null;
        }

        var image = new Image<Rgba32>(width, height);
        var start = height + 4;

        for (var y = 0; y < height; y++)
        {
            var lookupOffset = 8 + (y * 2);

            if (lookupOffset + 2 > source.Length)
            {
                break;
            }

            var lookup = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(lookupOffset, 2));
            var position = (start + lookup) * 2;
            var x = 0;

            while (position + 4 <= source.Length)
            {
                var xOffset = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(position, 2));
                position += 2;
                var xRun = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(position, 2));
                position += 2;

                if (xOffset + xRun == 0)
                {
                    break;
                }

                x += xOffset;

                for (var i = 0; i < xRun; i++)
                {
                    if (position + 2 > source.Length || x >= width)
                    {
                        break;
                    }

                    var value = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(position, 2));
                    position += 2;
                    value ^= 0x8000;

                    image[x, y] = ConvertArgb1555ToRgba(value);
                    x++;
                }
            }
        }

        return image;
    }

    private static Rgba32 ConvertArgb1555ToRgba(ushort value)
    {
        var a = (value & 0x8000) != 0 ? (byte)255 : (byte)0;
        var r = (byte)(((value >> 10) & 0x1F) * 255 / 31);
        var g = (byte)(((value >> 5) & 0x1F) * 255 / 31);
        var b = (byte)((value & 0x1F) * 255 / 31);

        return new(r, g, b, a);
    }
}
