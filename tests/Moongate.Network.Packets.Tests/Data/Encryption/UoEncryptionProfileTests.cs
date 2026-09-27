using Moongate.Network.Packets.Data.Encryption;
using Moongate.Network.Packets.Types.Encryption;

namespace Moongate.Network.Packets.Tests.Data.Encryption;

public sealed class UoEncryptionProfileTests
{
    [Fact]
    public void Parse_PolVersionBoundaries_SelectTheMatchingCipher()
    {
        (string Version, UoEncryptionType Type)[] cases =
        [
            ("none", UoEncryptionType.None), ("ignition", UoEncryptionType.None),
            ("uorice", UoEncryptionType.None), ("1.25.35", UoEncryptionType.OldBlowfish),
            ("1.25.36", UoEncryptionType.Blowfish12536), ("1.26.0", UoEncryptionType.Blowfish),
            ("2.0.0", UoEncryptionType.Blowfish), ("2.0.0x", UoEncryptionType.BlowfishTwofish),
            ("2.0.3", UoEncryptionType.BlowfishTwofish), ("2.0.4", UoEncryptionType.Twofish)
        ];
        foreach (var (version, type) in cases)
        {
            Assert.Equal(type, UoEncryptionProfile.Parse(version).Type);
        }
    }

    [Theory]
    [InlineData("7.0.117.0", 0x366150ADu, 0xAC9E5E7Fu, 7)]
    [InlineData("67.0.117.0", 0x146150ADu, 0xBD9E5E7Fu, 67)]
    [InlineData("2.0.0x", 0x2D13A5FDu, 0xA39D527Fu, 2)]
    public void Parse_PreservesWireVersionForPolMasterKeys(string version, uint key1, uint key2, int major)
    {
        var profile = UoEncryptionProfile.Parse(version);
        Assert.Equal(key1, profile.Key1);
        Assert.Equal(key2, profile.Key2);
        Assert.Equal(major, profile.Major);
    }

    [Theory]
    [InlineData("")]
    [InlineData("7.0")]
    [InlineData("7.0.bad")]
    [InlineData("-1.0.0")]
    [InlineData("7.0.1.0.0")]
    [InlineData("999999999999.0.1")]
    [InlineData("7.0.1garbage")]
    public void Parse_MalformedVersionsFail(string version)
    {
        Assert.Throws<FormatException>(() => UoEncryptionProfile.Parse(version));
    }
}
