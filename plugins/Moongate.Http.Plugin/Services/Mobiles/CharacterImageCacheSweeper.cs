using Moongate.Core.Interfaces;
using Serilog;
using SquidStd.Abstractions.Interfaces.Services;
using SquidStd.Core.Directories;

namespace Moongate.Http.Plugin.Services.Mobiles;

/// <summary>
/// Takes away character images nobody has looked at for a while.
/// <para>
/// Content addressing is what removes the need to invalidate anything, and this is its price: a
/// character who changes clothes leaves the previous picture behind. Unbounded growth proportional
/// to outfit changes is the same shape as any other leak, so it is swept rather than accepted.
/// </para>
/// </summary>
public sealed class CharacterImageCacheSweeper : ISquidStdService
{
    private const string TimerName = "character-image-sweep";
    private const string CacheDirectory = "cache/images/characters";

    /// <summary>How long a picture survives without being served. A swept one is simply re-rendered.</summary>
    private static readonly TimeSpan Window = TimeSpan.FromDays(7);

    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly ILogger _logger = Log.ForContext<CharacterImageCacheSweeper>();
    private readonly string _cachePath;
    private readonly IGameLoopContext? _loop;

    public CharacterImageCacheSweeper(DirectoriesConfig directories, IGameLoopContext? loop = null)
    {
        _cachePath = directories.RegisterDirectory(CacheDirectory);
        _loop = loop;
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        _loop?.ScheduleRepeating(TimerName, Interval, () => Sweep());

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    /// <summary>
    /// Removes pictures untouched for longer than the window, and returns how many went. Public so a
    /// test can drive one beat without waiting on the timer.
    /// </summary>
    /// <remarks>
    /// It reads last-*write* time on purpose, and the image service touches a file when it serves one
    /// from cache: last-access time is disabled or coarsened on many mounts, so a picture in daily
    /// use could otherwise look untouched for a week on a host mounted in a way we do not control.
    /// </remarks>
    public int Sweep()
    {
        if (!Directory.Exists(_cachePath))
        {
            return 0;
        }

        var cutoff = DateTime.UtcNow - Window;
        var removed = 0;

        foreach (var path in Directory.EnumerateFiles(_cachePath, "*.png"))
        {
            if (File.GetLastWriteTimeUtc(path) > cutoff)
            {
                continue;
            }

            try
            {
                File.Delete(path);
                removed++;
            }
            catch (IOException)
            {
                // Being read as it is swept is untidy, not a failure: it will go next time.
            }
        }

        if (removed > 0)
        {
            _logger.Debug("Swept {Count} character image(s) unread for more than {Days} days", removed, Window.Days);
        }

        return removed;
    }
}
