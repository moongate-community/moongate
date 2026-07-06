using Moongate.Tests.Support;
using Moongate.Ultima.Io;
using Moongate.Ultima.Localization;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class SpeechListTests
{
    [Fact]
    public void Initialize_SpeechFixture_ParsesIdKeywordAndOrder()
    {
        var speech = UltimaFixtures.BuildSpeech(((short)5, "*hello*"), ((short)9, "*goodbye*"));
        var dir = UltimaFixtures.CreateClientDirectory(("speech.mul", speech));

        try
        {
            Files.SetDirectory(dir);
            SpeechList.Initialize();

            Assert.Equal(2, SpeechList.Entries.Count);

            Assert.Equal(5, SpeechList.Entries[0].Id);
            Assert.Equal("*hello*", SpeechList.Entries[0].KeyWord);
            Assert.Equal(0, SpeechList.Entries[0].Order);

            Assert.Equal(9, SpeechList.Entries[1].Id);
            Assert.Equal("*goodbye*", SpeechList.Entries[1].KeyWord);
            Assert.Equal(1, SpeechList.Entries[1].Order);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Initialize_NoSpeechFile_YieldsEmptyList()
    {
        var dir = UltimaFixtures.CreateClientDirectory(("unrelated.txt", [0x00]));

        try
        {
            Files.SetDirectory(dir);
            SpeechList.Initialize();

            Assert.Empty(SpeechList.Entries);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
