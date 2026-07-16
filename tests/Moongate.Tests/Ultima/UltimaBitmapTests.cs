using Moongate.Ultima.Imaging;

namespace Moongate.Tests.Ultima;

public class UltimaBitmapTests
{
    private const ushort AlphaBit = 0x8000;
    private const ushort OpaqueWhite = 0xFFFF;
    private const ushort OpaqueRed = 0xFC00;

    [Fact]
    public unsafe void Clone_ModifyingCopy_DoesNotAffectOriginal()
    {
        using var original = new UltimaBitmap(2, 2);
        ((ushort*)original.Scan0)[0] = OpaqueRed;

        using var copy = original.Clone();
        ((ushort*)copy.Scan0)[0] = OpaqueWhite;

        Assert.Equal(OpaqueRed, ((ushort*)original.Scan0)[0]);
        Assert.Equal(OpaqueWhite, ((ushort*)copy.Scan0)[0]);
    }

    [Fact]
    public void Constructor_InvalidSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UltimaBitmap(0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UltimaBitmap(4, -1));
    }

    [Fact]
    public unsafe void Constructor_NewSurface_IsZeroed()
    {
        using var bmp = new UltimaBitmap(4, 4);

        var pixels = (ushort*)bmp.Scan0;

        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(0, pixels[i]);
        }
    }

    [Fact]
    public unsafe void DrawInto_CopiesOpaquePixels_SkipsTransparent()
    {
        using var src = new UltimaBitmap(2, 1);
        ((ushort*)src.Scan0)[0] = OpaqueRed;
        ((ushort*)src.Scan0)[1] = 0x7C00; // red without alpha bit -> transparent

        using var dest = new UltimaBitmap(2, 1);
        ((ushort*)dest.Scan0)[0] = OpaqueWhite;
        ((ushort*)dest.Scan0)[1] = OpaqueWhite;

        src.DrawInto(dest, 0, 0);

        Assert.Equal(OpaqueRed, ((ushort*)dest.Scan0)[0]);
        Assert.Equal(OpaqueWhite, ((ushort*)dest.Scan0)[1]);
    }

    [Fact]
    public unsafe void DrawInto_OutOfBounds_IsClipped()
    {
        using var src = new UltimaBitmap(2, 2);
        ((ushort*)src.Scan0)[0] = OpaqueRed;
        ((ushort*)src.Scan0)[3] = OpaqueRed;

        using var dest = new UltimaBitmap(2, 2);

        src.DrawInto(dest, 1, 1);

        Assert.Equal(0, ((ushort*)dest.Scan0)[0]);
        Assert.Equal(OpaqueRed, ((ushort*)dest.Scan0)[3]);
    }

    [Fact]
    public unsafe void FromImage_RoundTrip_PreservesOpaquePixels()
    {
        using var original = new UltimaBitmap(3, 2);
        ((ushort*)original.Scan0)[0] = OpaqueRed;
        ((ushort*)original.Scan0)[4] = OpaqueWhite;

        using var image = original.ToImage();
        using var roundTripped = UltimaBitmap.FromImage(image);

        Assert.Equal(OpaqueRed, ((ushort*)roundTripped.Scan0)[0]);
        Assert.Equal(OpaqueWhite, ((ushort*)roundTripped.Scan0)[4]);
        Assert.Equal(0, ((ushort*)roundTripped.Scan0)[1]);
    }

    [Fact]
    public unsafe void Save_And_FromFile_RoundTripsThroughPng()
    {
        var file = Path.Combine(Path.GetTempPath(), $"moongate-bmp-{Guid.NewGuid():N}.png");

        try
        {
            using var original = new UltimaBitmap(2, 2);
            ((ushort*)original.Scan0)[0] = OpaqueRed;
            ((ushort*)original.Scan0)[3] = OpaqueWhite;

            original.Save(file);
            using var loaded = UltimaBitmap.FromFile(file);

            Assert.Equal(2, loaded.Width);
            Assert.Equal(2, loaded.Height);
            Assert.Equal(OpaqueRed, ((ushort*)loaded.Scan0)[0]);
            Assert.Equal(OpaqueWhite, ((ushort*)loaded.Scan0)[3]);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Scan0_AfterDispose_Throws()
    {
        var bmp = new UltimaBitmap(1, 1);
        bmp.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bmp.Scan0);
    }

    [Fact]
    public void Stride_IsWidthTimesTwo_NoPadding()
    {
        using var bmp = new UltimaBitmap(5, 3);

        Assert.Equal(10, bmp.Stride);
    }

    [Fact]
    public unsafe void ToImage_ExpandsArgb1555ToBgra32()
    {
        using var bmp = new UltimaBitmap(2, 1);
        ((ushort*)bmp.Scan0)[0] = OpaqueWhite;
        ((ushort*)bmp.Scan0)[1] = 0;

        using var image = bmp.ToImage();

        Assert.Equal(new(255, 255, 255, 255), image[0, 0]);
        Assert.Equal((byte)0, image[1, 0].A);
    }

    [Fact]
    public unsafe void ToImage_Opaque_ForcesFullAlpha()
    {
        using var bmp = new UltimaBitmap(1, 1);
        ((ushort*)bmp.Scan0)[0] = 0x7C00; // red, no alpha bit

        using var image = bmp.ToImage(true);

        Assert.Equal((byte)255, image[0, 0].A);
        Assert.Equal((byte)255, image[0, 0].R);
    }
}
