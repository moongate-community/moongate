using Moongate.Tests.Support;
using Moongate.Ultima.Fonts;
using Moongate.Ultima.Io;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class AsciiFontTests
{
    [Fact]
    public void Initialize_FontFixture_ParsesCharacterBitmap()
    {
        var fonts = UltimaFixtures.BuildAsciiFonts(0, 'A', 3, 5, 0x1234);
        var dir = UltimaFixtures.CreateClientDirectory(("fonts.mul", fonts));

        try
        {
            Files.SetDirectory(dir);
            AsciiText.Initialize();

            var bitmap = AsciiText.Fonts[0].GetBitmap('A');

            Assert.NotNull(bitmap);
            Assert.Equal(3, bitmap.Width);
            Assert.Equal(5, bitmap.Height);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
