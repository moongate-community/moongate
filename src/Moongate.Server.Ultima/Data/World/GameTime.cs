namespace Moongate.Server.Ultima.Data.World;

/// <summary>
///     The time of day in the game, from 00:00 to 23:59.
/// </summary>
public sealed record GameTime(int Hours, int Minutes);
