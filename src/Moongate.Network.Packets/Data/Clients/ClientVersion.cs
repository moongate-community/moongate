using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Moongate.Network.Packets.Types.Clients;

namespace Moongate.Network.Packets.Data.Clients;

/// <summary>
///     The version a UO client reports, either as four numbers (packet 0xEF) or as text (packet 0xBD), such as
///     <c>7.0.117.0</c>. The Enhanced Client adds 60 to its major version; it is stored without the offset and marked
///     with <see cref="ClientType.Enhanced" />, so versions of both clients compare on the same scale.
/// </summary>
public sealed class ClientVersion : IEquatable<ClientVersion>, IComparable<ClientVersion>
{
    private const int KrMajor = 66;
    private const int EnhancedMajorOffset = 60;

    /// <summary>
    ///     The first version whose character list carries coordinates, map id and cliloc for every city.
    /// </summary>
    public static readonly ClientVersion Version70130 = new(7, 0, 13, 0);

    public int Major { get; }

    public int Minor { get; }

    public int Revision { get; }

    public int Patch { get; }

    public ClientType Type { get; }

    /// <param name="major">
    ///     The major version as the client reports it; 66 means Kingdom Reborn and 67 or more the Enhanced Client.
    /// </param>
    public ClientVersion(int major, int minor, int revision, int patch)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(major);
        ArgumentOutOfRangeException.ThrowIfNegative(minor);
        ArgumentOutOfRangeException.ThrowIfNegative(revision);
        ArgumentOutOfRangeException.ThrowIfNegative(patch);

        if (major > KrMajor)
        {
            Major = major - EnhancedMajorOffset;
            Type = ClientType.Enhanced;
        }
        else
        {
            Major = major;
            Type = major == KrMajor ? ClientType.Kr : ClientType.Classic;
        }

        Minor = minor;
        Revision = revision;
        Patch = patch;
    }

    /// <summary>
    ///     Parses the text a client sends, such as <c>7.0.117.0</c>, <c>7.0.117</c> or <c>67.0.105.0</c>.
    /// </summary>
    /// <exception cref="FormatException">
    ///     The text is not a client version.
    /// </exception>
    public static ClientVersion Parse(string text)
    {
        return TryParse(text, out var version)
            ? version
            : throw new FormatException($"'{text}' is not a client version.");
    }

    /// <summary>
    ///     Parses two to four dot-separated numbers; missing parts are 0. Old clients put the patch as a letter after
    ///     the revision (<c>4.0.7a</c>), which reads as patch 1 for <c>a</c>, 2 for <c>b</c> and so on.
    /// </summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out ClientVersion? version)
    {
        version = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Trim().Split('.');

        if (parts.Length is < 2 or > 4)
        {
            return false;
        }

        var numbers = new int[4];

        for (var index = 0; index < parts.Length; index++)
        {
            var part = parts[index];

            // Only the revision of a three-part version may carry the letter patch.
            if (index == 2 && parts.Length == 3 && part.Length > 1 && char.IsAsciiLetterLower(part[^1]))
            {
                numbers[3] = part[^1] - 'a' + 1;
                part = part[..^1];
            }

            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out numbers[index]))
            {
                return false;
            }
        }

        version = new(numbers[0], numbers[1], numbers[2], numbers[3]);

        return true;
    }

    public int CompareTo(ClientVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var result = Major.CompareTo(other.Major);

        if (result == 0)
        {
            result = Minor.CompareTo(other.Minor);
        }

        if (result == 0)
        {
            result = Revision.CompareTo(other.Revision);
        }

        // The Enhanced Client always reports patch 0, so its patch says nothing about the protocol.
        if (result == 0 && Type != ClientType.Enhanced && other.Type != ClientType.Enhanced)
        {
            result = Patch.CompareTo(other.Patch);
        }

        return result;
    }

    public bool Equals(ClientVersion? other)
    {
        return other is not null &&
               Major == other.Major &&
               Minor == other.Minor &&
               Revision == other.Revision &&
               Patch == other.Patch &&
               Type == other.Type;
    }

    public override bool Equals(object? obj)
    {
        return obj is ClientVersion other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Major, Minor, Revision, Patch, Type);
    }

    /// <summary>
    ///     Writes the version as the client reports it, always with four numbers, so <see cref="Parse" /> reads it
    ///     back unchanged.
    /// </summary>
    public override string ToString()
    {
        var major = Type == ClientType.Enhanced ? Major + EnhancedMajorOffset : Major;

        return string.Create(CultureInfo.InvariantCulture, $"{major}.{Minor}.{Revision}.{Patch}");
    }

    public static bool operator ==(ClientVersion? left, ClientVersion? right)
    {
        return left?.Equals(right) ?? right is null;
    }

    public static bool operator !=(ClientVersion? left, ClientVersion? right)
    {
        return !(left == right);
    }

    public static bool operator <(ClientVersion? left, ClientVersion? right)
    {
        return Compare(left, right) < 0;
    }

    public static bool operator <=(ClientVersion? left, ClientVersion? right)
    {
        return Compare(left, right) <= 0;
    }

    public static bool operator >(ClientVersion? left, ClientVersion? right)
    {
        return Compare(left, right) > 0;
    }

    public static bool operator >=(ClientVersion? left, ClientVersion? right)
    {
        return Compare(left, right) >= 0;
    }

    private static int Compare(ClientVersion? left, ClientVersion? right)
    {
        return left is null ? right is null ? 0 : -1 : left.CompareTo(right);
    }
}
