// /***************************************************************************
//  *
//  * "THE BEER-WARE LICENSE"
//  * As long as you retain this notice you can do whatever you want with
//  * this stuff. If we meet some day, and you think this stuff is worth it,
//  * you can buy me a beer in return.
//  *
//  ***************************************************************************/

using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Moongate.Ultima.Imaging;

/// <summary>
/// A 16-bit ARGB1555 pixel surface backed by native memory, mirroring the layout of the
/// GDI+ <c>Format16bppArgb1555</c> bitmaps used by the original Ultima SDK. <see cref="Scan0" />
/// and <see cref="Stride" /> follow the same semantics as System.Drawing's <c>BitmapData</c>,
/// so the SDK's unsafe pixel loops port unchanged. Convert with <see cref="ToImage" />
/// only when rendering or exporting is needed.
/// </summary>
public sealed unsafe class UltimaBitmap : IDisposable
{
    private const ushort AlphaBit = 0x8000;

    private nint _scan0;
    private bool _disposed;

    /// <summary>Surface width in pixels.</summary>
    public int Width { get; }

    /// <summary>Surface height in pixels.</summary>
    public int Height { get; }

    /// <summary>Row stride in bytes (always <c>Width * 2</c>, no padding).</summary>
    public int Stride { get; }

    /// <summary>Pointer to the first pixel of the first row.</summary>
    public nint Scan0
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return _scan0;
        }
    }

    public UltimaBitmap(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        Stride = width * sizeof(ushort);
        _scan0 = (nint)NativeMemory.AllocZeroed((nuint)(Stride * height));
    }

    ~UltimaBitmap()
    {
        if (!_disposed)
        {
            NativeMemory.Free((void*)_scan0);
        }
    }

    /// <summary>Creates a deep copy of this surface.</summary>
    public UltimaBitmap Clone()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var copy = new UltimaBitmap(Width, Height);
        NativeMemory.Copy((void*)_scan0, (void*)copy._scan0, (nuint)(Stride * Height));

        return copy;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        NativeMemory.Free((void*)_scan0);
        _scan0 = 0;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Blits this surface onto <paramref name="dest" /> at (<paramref name="dx" />, <paramref name="dy" />),
    /// skipping fully transparent pixels (alpha bit unset). Replaces the GDI+
    /// <c>Graphics.DrawImage</c> compositing used by the original SDK.
    /// </summary>
    public void DrawInto(UltimaBitmap dest, int dx, int dy)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(dest);

        for (var y = 0; y < Height; y++)
        {
            var ty = dy + y;

            if (ty < 0 || ty >= dest.Height)
            {
                continue;
            }

            var src = (ushort*)_scan0 + y * Width;
            var dst = (ushort*)dest._scan0 + ty * dest.Width;

            for (var x = 0; x < Width; x++)
            {
                var tx = dx + x;

                if (tx < 0 || tx >= dest.Width)
                {
                    continue;
                }

                var pixel = src[x];

                if ((pixel & AlphaBit) != 0)
                {
                    dst[tx] = pixel;
                }
            }
        }
    }

    /// <summary>Decodes an image file (PNG, BMP, JPEG, ...) into an ARGB1555 surface.</summary>
    public static UltimaBitmap FromFile(string fileName)
    {
        using var decoded = Image.Load<Bgra32>(fileName);

        return FromImage(decoded);
    }

    /// <summary>
    /// Converts a 32-bit BGRA image to an ARGB1555 surface. Pixels with alpha below 128
    /// become fully transparent (alpha bit unset).
    /// </summary>
    public static UltimaBitmap FromImage(Image<Bgra32> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var result = new UltimaBitmap(source.Width, source.Height);
        source.ProcessPixelRows(
            accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var src = MemoryMarshal.Cast<Bgra32, uint>(accessor.GetRowSpan(y));
                    var dst = (ushort*)result._scan0 + y * result.Width;

                    for (var x = 0; x < src.Length; x++)
                    {
                        dst[x] = Pack8888To1555(src[x]);
                    }
                }
            }
        );

        return result;
    }

    /// <summary>
    /// Encodes the surface to <paramref name="fileName" />; the format is picked from the
    /// file extension (png, bmp, tiff, jpeg, ...).
    /// </summary>
    public void Save(string fileName, bool opaque = false)
    {
        using var image = ToImage(opaque);
        image.Save(fileName);
    }

    /// <summary>
    /// Converts the ARGB1555 surface to a 32-bit BGRA image. Pass
    /// <paramref name="opaque" /> = true for surfaces produced without the alpha bit
    /// (e.g. map renders, originally RGB555): every pixel is emitted fully opaque.
    /// </summary>
    public Image<Bgra32> ToImage(bool opaque = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var image = new Image<Bgra32>(Width, Height);
        image.ProcessPixelRows(
            accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var dst = MemoryMarshal.Cast<Bgra32, uint>(accessor.GetRowSpan(y));
                    var src = (ushort*)_scan0 + y * Width;

                    for (var x = 0; x < Width; x++)
                    {
                        var pixel = Expand1555To8888(src[x]);

                        if (opaque)
                        {
                            pixel |= 0xFF000000u;
                        }

                        dst[x] = pixel;
                    }
                }
            }
        );

        return image;
    }

    private static uint Expand1555To8888(ushort pixel)
    {
        if (pixel == 0)
        {
            return 0;
        }

        var a = (pixel & AlphaBit) != 0 ? 0xFFu : 0x00u;
        var r = (uint)((pixel >> 10) & 0x1F);
        var g = (uint)((pixel >> 5) & 0x1F);
        var b = (uint)(pixel & 0x1F);

        r = (r << 3) | (r >> 2);
        g = (g << 3) | (g >> 2);
        b = (b << 3) | (b >> 2);

        return (a << 24) | (r << 16) | (g << 8) | b;
    }

    private static ushort Pack8888To1555(uint pixel)
    {
        var a = (pixel >> 24) & 0xFF;

        if (a < 128)
        {
            return 0;
        }

        var r = (pixel >> 16) & 0xFF;
        var g = (pixel >> 8) & 0xFF;
        var b = pixel & 0xFF;

        return (ushort)(AlphaBit | ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3));
    }
}
