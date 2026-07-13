using System.Runtime.CompilerServices;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Version;

/// <summary>
/// A parsed Ultima Online client version (major.minor.revision.patch plus client type),
/// with ordering, equality and the protocol-changes bracket derived from the version.
/// </summary>
public class ClientVersion : IComparable<ClientVersion>, IComparer<ClientVersion>, IEquatable<ClientVersion>
{
    public static readonly ClientVersion Version400a = new("4.0.0a");
    public static readonly ClientVersion Version407a = new("4.0.7a");
    public static readonly ClientVersion Version500a = new("5.0.0a");
    public static readonly ClientVersion Version502b = new("5.0.2b");
    public static readonly ClientVersion Version6000 = new("6.0.0.0");
    public static readonly ClientVersion Version6000KR = new("66.55.38"); // KR 2.44.0.15 (First release)
    public static readonly ClientVersion Version6017 = new("6.0.1.7");
    public static readonly ClientVersion Version6050 = new("6.0.5.0");
    public static readonly ClientVersion Version60142 = new("6.0.14.2");
    public static readonly ClientVersion Version60142KR = new("66.55.53"); // KR 2.59.0.2
    public static readonly ClientVersion Version7000 = new("7.0.0.0");
    public static readonly ClientVersion Version7090 = new("7.0.9.0");
    public static readonly ClientVersion Version70120 = new("7.0.12.0"); // Plant localization change
    public static readonly ClientVersion Version70130 = new("7.0.13.0");
    public static readonly ClientVersion Version70160 = new("7.0.16.0");
    public static readonly ClientVersion Version70300 = new("7.0.30.0");
    public static readonly ClientVersion Version70331 = new("7.0.33.1");
    public static readonly ClientVersion Version704565 = new("7.0.45.65");
    public static readonly ClientVersion Version70500 = new("7.0.50.0");
    public static readonly ClientVersion Version70610 = new("7.0.61.0");
    public static readonly ClientVersion Version70654 = new("7.0.65.4"); // Insufficient mana change

    public int Major { get; }

    public int Minor { get; }

    public int Revision { get; }

    public int Patch { get; }

    public ClientType Type { get; }

    public string SourceString { get; }

    public ProtocolChangesType ProtocolChangesType
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this switch
        {
            var v when v.Type is ClientType.KR && v >= Version60142KR => ProtocolChangesType.Version60142,
            var v when v.Type is ClientType.KR                        => ProtocolChangesType.Version6000,
            var v when v >= Version70610                              => ProtocolChangesType.Version70610,
            var v when v >= Version70500                              => ProtocolChangesType.Version70500,
            var v when v >= Version704565                             => ProtocolChangesType.Version704565,
            var v when v >= Version70331                              => ProtocolChangesType.Version70331,
            var v when v >= Version70300                              => ProtocolChangesType.Version70300,
            var v when v >= Version70160                              => ProtocolChangesType.Version70160,
            var v when v >= Version70130                              => ProtocolChangesType.Version70130,
            var v when v >= Version7090                               => ProtocolChangesType.Version7090,
            var v when v >= Version7000                               => ProtocolChangesType.Version7000,
            var v when v >= Version60142                              => ProtocolChangesType.Version60142,
            var v when v >= Version6017                               => ProtocolChangesType.Version6017,
            var v when v >= Version6000                               => ProtocolChangesType.Version6000,
            var v when v >= Version502b                               => ProtocolChangesType.Version502b,
            _                                                         => ProtocolChangesType.Version500a // We do not support versions lower than 5.0.0a
        };
    }

    public ClientVersion(int maj, int min, int rev, int pat, ClientType type = ClientType.Classic)
    {
        if (maj >= 67)
        {
            Major = maj - 60;
            Type = ClientType.SA;
        }
        else
        {
            Major = maj;
            Type = maj == 66 ? ClientType.KR : type;
        }

        Minor = min;
        Revision = rev;
        Patch = pat;

        SourceString = string.Intern(ToStringImpl());
    }

    public ClientVersion(string fmt)
    {
        fmt = fmt.ToLowerInvariant();
        SourceString = string.Intern(fmt);

        try
        {
            var br1 = fmt.IndexOf('.');
            var br2 = fmt.IndexOf('.', br1 + 1);

            var br3 = br2 + 1;

            while (br3 < fmt.Length && char.IsDigit(fmt, br3))
            {
                br3++;
            }

            Major = int.Parse(fmt.AsSpan()[..br1]);
            Minor = int.Parse(fmt.AsSpan(br1 + 1, br2 - br1 - 1));
            Revision = int.Parse(fmt.AsSpan(br2 + 1, br3 - br2 - 1));

            if (br3 < fmt.Length)
            {
                if (Major <= 5 && Minor <= 0 && Revision <= 6) // Anything before 5.0.7
                {
                    if (!char.IsWhiteSpace(fmt, br3))
                    {
                        Patch = fmt[br3] - 'a';
                    }
                }
                else
                {
                    Patch = int.Parse(fmt.AsSpan(br3 + 1, fmt.Length - br3 - 1));
                }
            }

            if (Major == 66)
            {
                Type = ClientType.KR;
            }
            else if (Major > 66)
            {
                Major -= 60;
                Type = ClientType.SA;
            }
            else if (fmt.Contains("third dawn") ||
                     fmt.Contains("uo:td") ||
                     fmt.Contains("uotd") ||
                     fmt.Contains("uo3d") ||
                     fmt.Contains("uo:3d"))
            {
                Type = ClientType.UOTD;
            }
        }
        catch
        {
            Major = 0;
            Minor = 0;
            Revision = 0;
            Patch = 0;
            Type = ClientType.Classic;
        }
    }

    /// <summary>
    /// Builds a version from a single packed uint whose four bytes are, high to low,
    /// major, minor, revision and patch (e.g. 0x07004104 -> 7.0.65.4).
    /// </summary>
    public static ClientVersion FromPacked(uint packed)
    {
        var major = (byte)(packed >> 24);
        var minor = (byte)(packed >> 16);
        var revision = (byte)(packed >> 8);
        var patch = (byte)packed;

        return new ClientVersion(major, minor, revision, patch);
    }

    public static int Compare(ClientVersion? a, ClientVersion? b)
    {
        if (a is null && b is null)
        {
            return 0;
        }

        if (a is null)
        {
            return -1;
        }

        if (b is null)
        {
            return 1;
        }

        return a.CompareTo(b);
    }

    public int CompareTo(ClientVersion? o)
    {
        if (o == null)
        {
            return 1;
        }

        if (Major > o.Major)
        {
            return 1;
        }

        if (Major < o.Major)
        {
            return -1;
        }

        if (Minor > o.Minor)
        {
            return 1;
        }

        if (Minor < o.Minor)
        {
            return -1;
        }

        if (Revision > o.Revision)
        {
            return 1;
        }

        if (Revision < o.Revision)
        {
            return -1;
        }

        // Don't test patch for EC since it is always 0 but compatible with classic non-zero
        if (Type == ClientType.SA || o.Type == ClientType.SA)
        {
            return 0;
        }

        if (Patch > o.Patch)
        {
            return 1;
        }

        if (Patch < o.Patch)
        {
            return -1;
        }

        return 0;
    }

    public bool Equals(ClientVersion? other)
    {
        return !ReferenceEquals(null, other) &&
               (ReferenceEquals(this, other) ||
                Major == other.Major &&
                Minor == other.Minor &&
                Revision == other.Revision &&
                Patch == other.Patch &&
                Type == other.Type);
    }

    public override bool Equals(object? obj)
    {
        return !ReferenceEquals(null, obj) &&
               (ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((ClientVersion)obj));
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Major, Minor, Revision, Patch);
    }

    public static bool IsNull(object? x)
    {
        return ReferenceEquals(x, null);
    }

    public static bool operator ==(ClientVersion? l, ClientVersion? r)
    {
        return Equals(l, r);
    }

    public static bool operator >(ClientVersion? l, ClientVersion? r)
    {
        return Compare(l, r) > 0;
    }

    public static bool operator >=(ClientVersion? l, ClientVersion? r)
    {
        return Compare(l, r) >= 0;
    }

    public static bool operator !=(ClientVersion? l, ClientVersion? r)
    {
        return !Equals(l, r);
    }

    public static bool operator <(ClientVersion? l, ClientVersion? r)
    {
        return Compare(l, r) < 0;
    }

    public static bool operator <=(ClientVersion? l, ClientVersion? r)
    {
        return Compare(l, r) <= 0;
    }

    public override string ToString()
    {
        return SourceString;
    }

    int IComparer<ClientVersion>.Compare(ClientVersion? x, ClientVersion? y)
    {
        return Compare(x, y);
    }

    private string ToStringImpl()
    {
        string result;

        if (Type == ClientType.SA)
        {
            result = $"{Major + 60:00}.{Minor:00}.{Revision:00}";
        }
        else if (Major > 5 || Minor > 0 || Revision > 6)
        {
            result = $"{Major}.{Minor}.{Revision}.{Patch}";
        }
        else if (Patch > 0)
        {
            result = $"{Major}.{Minor}.{Revision}{(char)('a' + (Patch - 1))}";
        }
        else
        {
            result = $"{Major}.{Minor}.{Revision}";
        }

        if (Type == ClientType.UOTD)
        {
            result += " uotd";
        }

        return result;
    }
}
