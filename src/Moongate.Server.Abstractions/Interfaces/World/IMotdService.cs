namespace Moongate.Server.Abstractions.Interfaces.World;

/// <summary>The message of the day, as the lines a player is shown on entering the world.</summary>
public interface IMotdService
{
    /// <summary>
    /// The greeting, one entry per system message: the shard's version, how many players are online,
    /// and the operator's own text from <c>motd.txt</c>. Read fresh each time, so editing the file
    /// takes effect without a restart.
    /// </summary>
    IReadOnlyList<string> Lines();
}
