using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Ultima.Io;
using Serilog;
using SquidStd.Abstractions.Interfaces.Services;
using SquidStd.Core.Interfaces.Events;
using Moongate.Ultima.Helpers;
using Moongate.Ultima.Maps;

namespace Moongate.Server.Services.Loading;

/// <summary>
/// Points the Ultima file locator at the configured client directory on startup and announces
/// readiness on the event bus via <see cref="FilesLoadedEvent" />.
/// </summary>
public sealed class FilesLoaderService : ISquidStdService
{
    private readonly ILogger _logger = Log.ForContext<FilesLoaderService>();
    private readonly MoongateConfig _config;
    private readonly IEventBus _eventBus;

    public FilesLoaderService(MoongateConfig config, IEventBus eventBus)
    {
        _config = config;
        _eventBus = eventBus;
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        Files.SetDirectory(_config.UltimaDirectory);

        // Modern client installs ship a separate map1 for Trammel; without this, Trammel is read from
        // Felucca's file — the RunUO-era sharing — and the server lives on a different world than the
        // client wherever the two maps diverge. New Haven is the loud case: the client stands in a
        // city, the server sees Ocllo's forest, and no item the shard places there is ever sent.
        // The helper has existed, uncalled, since the UOFiddler port.
        MapHelper.CheckForNewMapSize();

        _logger.Information(
            "Trammel reads map file {FileIndex}",
            Map.Trammel.FileIndex
        );

        var fileCount = Files.MulPath?.Values.Count(path => !string.IsNullOrEmpty(path)) ?? 0;

        _logger.Information(
            "UO client files located in {Directory} ({FileCount} files)",
            _config.UltimaDirectory,
            fileCount
        );
        _eventBus.Publish(new FilesLoadedEvent(_config.UltimaDirectory, fileCount));

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
