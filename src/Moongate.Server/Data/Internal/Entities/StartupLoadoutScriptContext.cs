namespace Moongate.Server.Data.Internal.Entities;

/// <summary>
/// Carries the Lua-facing context for startup loadout generation.
/// </summary>
public sealed class StartupLoadoutScriptContext
{
    /// <summary>
    /// Gets or sets player name.
    /// </summary>
    public required string PlayerName { get; init; }

    /// <summary>
    /// Gets or sets normalized race name.
    /// </summary>
    public required string Race { get; init; }

    /// <summary>
    /// Gets or sets normalized gender name.
    /// </summary>
    public required string Gender { get; init; }

    /// <summary>
    /// Gets or sets normalized profession name.
    /// </summary>
    public required string Profession { get; init; }
}
