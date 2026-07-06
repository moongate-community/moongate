using Moongate.Tests.Support;
using Moongate.Ultima.Audio;
using Moongate.Ultima.Io;

namespace Moongate.Tests.Ultima;

[Collection("UltimaClientData")]
public class SoundsTests
{
    [Fact]
    public void GetSound_SoundFixture_ReturnsNameIdAndWavBuffer()
    {
        var audio = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var (idx, mul) = UltimaFixtures.BuildSounds((1, "FootStep", audio));
        var dir = UltimaFixtures.CreateClientDirectory(("soundidx.mul", idx), ("sound.mul", mul));

        try
        {
            Files.SetDirectory(dir);
            Sounds.Initialize();

            var sound = Sounds.GetSound(1);

            Assert.NotNull(sound);
            Assert.Equal(1, sound.Id);
            Assert.Equal("FootStep", sound.Name);
            Assert.True(sound.Buffer.Length > audio.Length); // 44-byte WAV header prepended to the PCM data
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void IsValidSound_KnownAndUnknownIds()
    {
        var (idx, mul) = UltimaFixtures.BuildSounds((1, "FootStep", [1, 2, 3, 4]));
        var dir = UltimaFixtures.CreateClientDirectory(("soundidx.mul", idx), ("sound.mul", mul));

        try
        {
            Files.SetDirectory(dir);
            Sounds.Initialize();

            Assert.True(Sounds.IsValidSound(1, out _, out _));
            Assert.False(Sounds.IsValidSound(50, out _, out _));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
