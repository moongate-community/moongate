namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     The ghost body of each human, elf and gargoyle body, and the way back. A player is dead when it wears one.
/// </summary>
internal static class GhostBodies
{
    private static readonly Dictionary<int, int> Ghosts = new()
    {
        [0x190] = 0x192,
        [0x191] = 0x193,
        [0x25D] = 0x25F,
        [0x25E] = 0x260,
        [0x29A] = 0x2B6,
        [0x29B] = 0x2B7
    };

    private static readonly Dictionary<int, int> Living = Ghosts.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>
    ///     Gets the ghost body of a living body; the body itself when it has none.
    /// </summary>
    public static int GhostOf(int body)
    {
        return Ghosts.GetValueOrDefault(body, body);
    }

    /// <summary>
    ///     Gets the living body of a ghost body; the body itself when it is not a ghost.
    /// </summary>
    public static int LivingOf(int body)
    {
        return Living.GetValueOrDefault(body, body);
    }

    /// <summary>
    ///     Gets whether the body is a ghost body.
    /// </summary>
    public static bool IsGhost(int body)
    {
        return Living.ContainsKey(body);
    }
}
