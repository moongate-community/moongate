using Moongate.Server.Core.Types.Accounts;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Support.Serialization.Data;

/// <summary>
///     A TOML document with a plain enum, an optional one and a flags one, each followed by another key.
/// </summary>
public sealed class EnumHolder
{
    public AccountType Account { get; set; }

    public string After { get; set; } = "";

    public AccountType? Optional { get; set; }

    public TileFlagType Flags { get; set; }

    public int Last { get; set; }
}
