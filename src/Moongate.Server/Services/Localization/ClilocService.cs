using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Interfaces.Localization;
using Moongate.Ultima.Localization;
using Serilog;
using SquidStd.Abstractions.Interfaces.Services;

namespace Moongate.Server.Services.Localization;

/// <summary>
/// Loads one language's cliloc table at startup and answers lookups from it. Started after
/// <c>FilesLoaderService</c> has pointed the reader at the client directory, so a missing or
/// unreadable table is a complaint at boot rather than a surprise mid-game — and a complaint, not a
/// crash: the shard runs without it and callers fall back to showing template ids.
/// </summary>
public sealed class ClilocService : IClilocService, ISquidStdService
{
    private readonly ILogger _logger = Log.ForContext<ClilocService>();
    private readonly MoongateConfig _config;

    private Dictionary<int, string> _text = [];

    public ClilocService(MoongateConfig config)
    {
        _config = config;
    }

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var list = new StringList(_config.Language, false);
            var text = new Dictionary<int, string>(list.Entries.Count);

            // Indexer rather than ToDictionary: a table with a repeated number should lose the
            // duplicate, not fail the boot.
            foreach (var entry in list.Entries)
            {
                text[entry.Number] = entry.Text;
            }

            _text = text;

            if (!string.IsNullOrEmpty(list.LoadWarning))
            {
                _logger.Warning("Cliloc table loaded with a warning: {Warning}", list.LoadWarning);
            }

            _logger.Information(
                "Loaded {EntryCount} cliloc entries for language {Language}",
                _text.Count,
                _config.Language
            );
        }
        catch (Exception exception)
        {
            _logger.Warning(
                exception,
                "No cliloc table for language {Language}; items with no name of their own will show their template id",
                _config.Language
            );
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public string? Text(int cliloc)
        => _text.GetValueOrDefault(cliloc);
}
