using System.Runtime.InteropServices;
using SkiaSharp;
using Moongate.Ultima.Catalog;
using Moongate.Ultima.Imaging;

namespace Moongate.Tests.Ultima.Catalog;

public class ItemCatalogTests
{
    [Fact]
    public void EncodePng_ReturnedStream_IsReadableFromStartAndDoesNotOwnSource()
    {
        using var bitmap = new UltimaBitmap(2, 1);
        Marshal.WriteInt16(bitmap.Scan0, unchecked((short)0x801F));

        using (var stream = ItemCatalog.EncodePng(bitmap))
        {
            Assert.Equal(0, stream.Position);
            Assert.True(stream.CanRead);
            using var decoded = SKBitmap.Decode(stream);
            Assert.NotNull(decoded);
            Assert.Equal(2, decoded.Width);
            Assert.Equal(1, decoded.Height);
            Assert.Equal(new SKColor(0, 0, 255), decoded.GetPixel(0, 0));
            Assert.Equal(new SKColor(0, 0, 0, 0), decoded.GetPixel(1, 0));
        }

        Assert.Equal(unchecked((short)0x801F), Marshal.ReadInt16(bitmap.Scan0));
        using var secondStream = ItemCatalog.EncodePng(bitmap);
        Assert.True(secondStream.Length > 0);
    }
}
