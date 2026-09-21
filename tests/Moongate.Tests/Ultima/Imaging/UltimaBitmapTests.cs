using System.Runtime.InteropServices;
using Moongate.Ultima.Imaging;
using SkiaSharp;

namespace Moongate.Tests.Ultima.Imaging;

public class UltimaBitmapTests
{
    [Fact]
    public void FromFile_InvalidImage_ThrowsInvalidDataException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"moongate-{Guid.NewGuid():N}.png");

        try
        {
            File.WriteAllBytes(path, [1, 2, 3, 4]);

            Assert.Throws<InvalidDataException>(() => UltimaBitmap.FromFile(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FromFile_SemitransparentPng_QuantizesOriginalStraightAlphaColors()
    {
        var path = Path.Combine(Path.GetTempPath(), $"moongate-{Guid.NewGuid():N}.png");

        try
        {
            // Independently encoded 1x1 RGBA PNG: (7, 15, 23, 128).
            File.WriteAllBytes(
                path,
                Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGNg5xdvAAAA/ACuC2M2gAAAAABJRU5ErkJggg=="
                )
            );

            using var bitmap = UltimaBitmap.FromFile(path);

            Assert.Equal(unchecked((short)0x8022), Marshal.ReadInt16(bitmap.Scan0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FromImage_OpaqueBitmap_IgnoresUnusedAlphaBytes()
    {
        using var source = new SKBitmap(new(1, 1, SKColorType.Bgra8888, SKAlphaType.Opaque));
        Marshal.Copy(new byte[] { 0, 0, 255, 0 }, 0, source.GetPixels(), 4);

        using var bitmap = UltimaBitmap.FromImage(source);

        Assert.Equal(unchecked((short)0xFC00), Marshal.ReadInt16(bitmap.Scan0));
    }

    [Fact]
    public void FromImage_PremultipliedBgra_RestoresColorBeforeQuantization()
    {
        using var source = new SKBitmap(new(1, 1, SKColorType.Bgra8888, SKAlphaType.Premul));
        Marshal.Copy(new byte[] { 0, 0, 128, 128 }, 0, source.GetPixels(), 4);

        using var bitmap = UltimaBitmap.FromImage(source);

        Assert.Equal(unchecked((short)0xFC00), Marshal.ReadInt16(bitmap.Scan0));
    }

    [Fact]
    public void FromImage_RgbaWithPaddedRows_PreservesChannelOrderAndAlphaThreshold()
    {
        var info = new SKImageInfo(3, 2, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var source = new SKBitmap(info, 20);
        byte[] pixels =
        [
            255, 0, 0, 127, 0, 255, 0, 128, 0, 0, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, // Row padding must not become pixels.
            8, 16, 24, 255, 255, 255, 255, 0, 0, 0, 0, 255
        ];
        Marshal.Copy(pixels, 0, source.GetPixels(), pixels.Length);

        using var bitmap = UltimaBitmap.FromImage(source);

        var actual = new short[6];
        Marshal.Copy(bitmap.Scan0, actual, 0, actual.Length);
        Assert.Equal(
            new short[]
            {
                0, unchecked((short)0x83E0), unchecked((short)0x801F),
                unchecked((short)0x8443), 0, unchecked((short)0x8000)
            },
            actual
        );
    }

    [Fact]
    public void Save_Png_RoundTripsOpaqueBlackAndTransparentPixels()
    {
        var path = Path.Combine(Path.GetTempPath(), $"moongate-{Guid.NewGuid():N}.png");

        try
        {
            using var bitmap = new UltimaBitmap(3, 1);
            Marshal.WriteInt16(bitmap.Scan0, 0, unchecked((short)0xFC00));
            Marshal.WriteInt16(bitmap.Scan0, 2, unchecked((short)0x8000));

            bitmap.Save(path);

            using var decoded = SKBitmap.Decode(path);
            Assert.NotNull(decoded);
            Assert.Equal(new(255, 0, 0), decoded.GetPixel(0, 0));
            Assert.Equal(new(0, 0, 0), decoded.GetPixel(1, 0));
            Assert.Equal(new(0, 0, 0, 0), decoded.GetPixel(2, 0));

            using var restored = UltimaBitmap.FromFile(path);
            Assert.Equal(unchecked((short)0xFC00), Marshal.ReadInt16(restored.Scan0, 0));
            Assert.Equal(unchecked((short)0x8000), Marshal.ReadInt16(restored.Scan0, 2));
            Assert.Equal(0, Marshal.ReadInt16(restored.Scan0, 4));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory,
     InlineData(".png", SKEncodedImageFormat.Png),
     InlineData(".PNG", SKEncodedImageFormat.Png),
     InlineData(".jpg", SKEncodedImageFormat.Jpeg),
     InlineData(".jpeg", SKEncodedImageFormat.Jpeg),
     InlineData(".webp", SKEncodedImageFormat.Webp)]
    public void Save_SupportedExtension_WritesMatchingImageFormat(string extension, SKEncodedImageFormat expectedFormat)
    {
        var path = Path.Combine(Path.GetTempPath(), $"moongate-{Guid.NewGuid():N}{extension}");

        try
        {
            using var bitmap = new UltimaBitmap(1, 1);
            Marshal.WriteInt16(bitmap.Scan0, unchecked((short)0xFFFF));

            bitmap.Save(path);

            using var codec = SKCodec.Create(path);
            Assert.NotNull(codec);
            Assert.Equal(expectedFormat, codec.EncodedFormat);
            using var restored = UltimaBitmap.FromFile(path);
            Assert.Equal(1, restored.Width);
            Assert.Equal(1, restored.Height);
            Assert.Equal(unchecked((short)0xFFFF), Marshal.ReadInt16(restored.Scan0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory, InlineData(".bmp"), InlineData(".tiff")]
    public void Save_UnsupportedExtension_PreservesExistingFile(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"moongate-{Guid.NewGuid():N}{extension}");
        byte[] original = [1, 2, 3, 4];

        try
        {
            File.WriteAllBytes(path, original);
            using var bitmap = new UltimaBitmap(1, 1);

            Assert.Throws<NotSupportedException>(() => bitmap.Save(path));

            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ToImage_Argb1555Pixels_PreservesColorsTransparencyAndRows()
    {
        using var bitmap = new UltimaBitmap(3, 2);
        Marshal.Copy(
            new short[]
            {
                unchecked((short)0xFC00), unchecked((short)0x83E0), unchecked((short)0x801F),
                0, unchecked((short)0x8000), unchecked((short)0xFFFF)
            },
            0,
            bitmap.Scan0,
            6
        );

        using var image = bitmap.ToImage();

        Assert.Equal(3, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(new(255, 0, 0), image.GetPixel(0, 0));
        Assert.Equal(new(0, 255, 0), image.GetPixel(1, 0));
        Assert.Equal(new(0, 0, 255), image.GetPixel(2, 0));
        Assert.Equal(new(0, 0, 0, 0), image.GetPixel(0, 1));
        Assert.Equal(new(0, 0, 0), image.GetPixel(1, 1));
        Assert.Equal(new(255, 255, 255), image.GetPixel(2, 1));
    }

    [Fact]
    public void ToImage_DisposedSurface_ThrowsObjectDisposedException()
    {
        var bitmap = new UltimaBitmap(1, 1);
        bitmap.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bitmap.ToImage());
    }

    [Fact]
    public void ToImage_OpaqueRgb555_PreservesColorsWithoutAlphaBit()
    {
        using var bitmap = new UltimaBitmap(2, 1);
        Marshal.WriteInt16(bitmap.Scan0, 0, 0x7C00);

        using var image = bitmap.ToImage(true);

        Assert.Equal(new(255, 0, 0), image.GetPixel(0, 0));
        Assert.Equal(new(0, 0, 0), image.GetPixel(1, 0));
    }
}
