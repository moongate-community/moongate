using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Ultima.Io;
using Serilog;
using SquidStd.Abstractions.Interfaces.Services;
using SquidStd.Core.Interfaces.Events;

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
