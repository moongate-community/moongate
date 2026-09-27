using System.Globalization;
using Moongate.Network.Packets.Types.Encryption;

namespace Moongate.Network.Packets.Data.Encryption;

/// <summary>Immutable POL cipher selection and public protocol keys derived from the raw wire version.</summary>
public sealed record UoEncryptionProfile
{
    public UoEncryptionType Type { get; }
    public uint Key1 { get; }
    public uint Key2 { get; }
    public int Major { get; }
    public int Minor { get; }
    public int Revision { get; }
    public int Patch { get; }

    private UoEncryptionProfile(UoEncryptionType type, uint key1, uint key2, int major, int minor, int revision, int patch)
    {
        Type = type;
        Key1 = key1;
        Key2 = key2;
        Major = major;
        Minor = minor;
        Revision = revision;
        Patch = patch;
    }

    /// <summary>Parses a POL version without removing the Enhanced Client's major-version offset.</summary>
    public static UoEncryptionProfile Parse(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new FormatException("An encryption client version is required.");
        }

        version = version.Trim();
        if (version.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            version.Equals("ignition", StringComparison.OrdinalIgnoreCase) ||
            version.Equals("uorice", StringComparison.OrdinalIgnoreCase))
        {
            return new(UoEncryptionType.None, 0, 0, 0, 0, 0, 0);
        }

        if (version.Equals("2.0.0x", StringComparison.OrdinalIgnoreCase))
        {
            return new(UoEncryptionType.BlowfishTwofish, 0x2D13A5FD, 0xA39D527F, 2, 0, 0, 24);
        }

        var parts = version.Split('.');
        if (parts.Length is < 3 or > 4)
        {
            throw new FormatException("Encryption versions require three or four components.");
        }

        Span<int> numbers = stackalloc int[4];
        numbers.Clear();
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (i == 2 && parts.Length == 3 && part.Length > 1 && char.IsAsciiLetterLower(part[^1]))
            {
                numbers[3] = part[^1] - 'a' + 1;
                part = part[..^1];
            }

            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                throw new FormatException("Encryption version components must be nonnegative integers.");
            }
        }

        var (major, minor, revision, patch) = (numbers[0], numbers[1], numbers[2], numbers[3]);
        var comparable = new Version(major, minor, revision);
        var type = comparable <= new Version(1, 25, 35) ? UoEncryptionType.OldBlowfish
            : comparable == new Version(1, 25, 36) ? UoEncryptionType.Blowfish12536
            : comparable <= new Version(2, 0, 0) ? UoEncryptionType.Blowfish
            : comparable <= new Version(2, 0, 3) ? UoEncryptionType.BlowfishTwofish
            : UoEncryptionType.Twofish;

        unchecked
        {
            var a = (uint)major;
            var b = (uint)minor;
            var c = (uint)revision;
            var temp = ((a << 9 | b) << 10 | c) ^ ((c * c) << 5);
            var key1 = (temp << 4) ^ (b * b) ^ (b * 0x0B000000) ^ (c * 0x380000) ^ 0x2C13A5FD;
            temp = (((a << 9 | c) << 10 | b) * 8) ^ (c * c * 0x0C00);
            var key2 = temp ^ (b * b) ^ (b * 0x06800000) ^ (c * 0x1C0000) ^ 0xA31D527F;

            return new(type, key1, key2, major, minor, revision, patch);
        }
    }
}
