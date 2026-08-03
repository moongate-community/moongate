using System.Globalization;
using Moongate.Server.Abstractions.Interfaces.World;
using SquidStd.Core.Directories;

namespace Moongate.Server.Services.World;

/// <summary>
/// The greeting a player meets on entering the world: what the shard is running, how many people
/// are here, and whatever the operator wrote in <c>motd.txt</c>.
///
/// The file sits at the root of the runtime directory, beside <c>moongate.yaml</c>, because that is
/// where an operator looks. It is read on each greeting rather than cached, so editing it takes
/// effect without a restart — which is the whole point of it being a file.
/// </summary>
public sealed class MotdService : IMotdService
{
    /// <summary>What a shard that has never been configured says, so the file is worth finding.</summary>
    private const string SampleMotd = "Welcome to Moongate. Edit motd.txt to change this message.";

    private readonly string _path;
    private readonly string _version;
    private readonly Func<int> _onlinePlayers;

    public MotdService(DirectoriesConfig directories, string version, Func<int> onlinePlayers)
    {
        _path = Path.Combine(directories.Root, "motd.txt");
        _version = version;
        _onlinePlayers = onlinePlayers;
    }

    public IReadOnlyList<string> Lines()
    {
        List<string> lines =
        [
            $"Moongate v{_version}",
            string.Create(CultureInfo.InvariantCulture, $"Total online players: {_onlinePlayers()}"),
        ];

        lines.AddRange(Body());

        return lines;
    }

    /// <summary>
    /// The operator's own text, one entry per line: the client shows one line at a time, so a
    /// paragraph must arrive as several messages rather than one long one. Blank lines are dropped —
    /// a file left with trailing newlines should not greet anyone with silence.
    /// </summary>
    private IEnumerable<string> Body()
    {
        if (!File.Exists(_path))
        {
            // First boot: leave the file behind so the operator finds it and edits it, rather than
            // having to learn that it could exist. Same courtesy moongate.yaml already gets.
            File.WriteAllText(_path, SampleMotd + Environment.NewLine);

            return [SampleMotd];
        }

        return File.ReadAllLines(_path)
                   .Select(line => line.Trim())
                   .Where(line => line.Length > 0);
    }
}
