using System.Collections.Concurrent;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Jail;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Types.Jail;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the jail: the cells of <c>data/jail.toml</c> and the sentences of <c>world.jail_sentences</c>, read at
///     startup and written by the world save. One repeating <c>jail</c> timer looks every ten seconds for the
///     sentences that are over.
/// </summary>
public sealed class JailService : IJailService
{
    public const string TimerName = "jail";
    public const int JailedMessage = 30138;

    private const long MillisecondsADay = 86_400_000;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);

    private readonly ILogger _logger = Log.ForContext<JailService>();
    private readonly IDataLoaderService _data;
    private readonly IDataAccess<JailSentenceEntity> _table;
    private readonly IMobileService _mobiles;
    private readonly ISessionService _sessions;
    private readonly ITeleportService _teleports;
    private readonly ISpeechService _speech;
    private readonly ITimerService _timers;
    private readonly JailConfig _config;
    private readonly TimeProvider _time;
    private readonly ILocalizationService? _localization;

    private readonly Dictionary<Serial, JailSentenceEntity> _sentences = new();

    // The sentences that ended since the last world save, which deletes their rows.
    private readonly ConcurrentDictionary<Serial, byte> _ended = new();

    private JailFile? _file;
    private string? _timerId;

    public bool IsEnabled => _file is not null;

    public IReadOnlyList<JailCell> Cells => _file?.Cell ?? [];

    public IReadOnlyCollection<JailSentenceEntity> Sentences => _sentences.Values;

    public int MaxDays => _config.MaxDays;

    public JailService(
        IDataLoaderService data,
        IDataAccess<JailSentenceEntity> table,
        IMobileService mobiles,
        ISessionService sessions,
        ITeleportService teleports,
        ISpeechService speech,
        ITimerService timers,
        JailConfig config,
        TimeProvider time,
        ILocalizationService? localization = null
    )
    {
        _data = data;
        _table = table;
        _mobiles = mobiles;
        _sessions = sessions;
        _teleports = teleports;
        _speech = speech;
        _timers = timers;
        _config = config;
        _time = time;
        _localization = localization;
    }

    public async Task StartAsync()
    {
        _file = _data.GetEntities<JailFile>().FirstOrDefault();

        foreach (var sentence in await _table.GetAllAsync())
        {
            _sentences[sentence.Id] = sentence;
        }

        _timerId = _timers.RegisterTimer(TimerName, CheckInterval, Check, CheckInterval, true);
        _logger.Information("Loaded {Count} jail sentences", _sentences.Count);
    }

    public Task StopAsync()
    {
        if (_timerId is { } id)
        {
            _timers.UnregisterTimer(id);
            _timerId = null;
        }

        return Task.CompletedTask;
    }

    public JailSentenceEntity? GetSentence(Serial prisoner)
    {
        return _sentences.GetValueOrDefault(prisoner);
    }

    public JailSentenceEntity? GetOccupant(int cell)
    {
        var now = Now();

        return _sentences.Values.FirstOrDefault(sentence => sentence.Cell == cell && !sentence.IsOver(now));
    }

    public JailResultType Jail(MobileEntity prisoner, int cell, int days, MobileEntity by)
    {
        if (_file is not { } file)
        {
            return JailResultType.Disabled;
        }

        if (days < 1 || days > _config.MaxDays)
        {
            return JailResultType.BadDays;
        }

        if (file.Cell.FirstOrDefault(candidate => candidate.Number == cell) is not { } target)
        {
            return JailResultType.NoSuchCell;
        }

        if (!_mobiles.IsInWorld(prisoner.Id))
        {
            return JailResultType.NotInWorld;
        }

        if (prisoner.Id == by.Id || OutranksOrEquals(prisoner, by))
        {
            return JailResultType.Refused;
        }

        if (GetOccupant(cell) is { } occupant && occupant.Id != prisoner.Id)
        {
            return JailResultType.CellOccupied;
        }

        // Where it stands now, read before the teleport moves it.
        var (map, location) = (prisoner.Map, prisoner.Location);

        if (!_teleports.Teleport(prisoner, file.Map, target.Location))
        {
            return JailResultType.MapNotLoaded;
        }

        var now = Now();

        if (!_sentences.TryGetValue(prisoner.Id, out var sentence))
        {
            sentence = new JailSentenceEntity
            {
                Id = prisoner.Id, ReturnMap = map, ReturnX = location.X, ReturnY = location.Y, ReturnZ = location.Z
            };
            _sentences[prisoner.Id] = sentence;
            _ended.TryRemove(prisoner.Id, out _);
        }

        sentence.Name = prisoner.Name;
        sentence.IsPlayer = !prisoner.IsNpc;
        sentence.Cell = cell;
        sentence.Days = days;
        sentence.JailedAt = now;
        sentence.ReleaseAt = now + days * MillisecondsADay;
        sentence.JailedBy = by.Name;
        sentence.Pardoned = false;
        _speech.Tell(prisoner, _localization.Text(JailedMessage, "You have been jailed for {0} days.", days));

        return JailResultType.Ok;
    }

    public IReadOnlyCollection<Serial> Capture()
    {
        return _ended.Keys.ToArray();
    }

    public void Committed(IReadOnlyCollection<Serial> serials)
    {
        foreach (var serial in serials)
        {
            _ended.TryRemove(serial, out _);
        }
    }

    private void Check()
    {
    }

    private long Now()
    {
        return _time.GetUtcNow().ToUnixTimeMilliseconds();
    }

    // A player of the jailer's rank or above is not jailed by it; an NPC has no rank.
    private bool OutranksOrEquals(MobileEntity prisoner, MobileEntity by)
    {
        return _sessions.TryGetByCharacterId(prisoner.Id, out var theirs) &&
               _sessions.TryGetByCharacterId(by.Id, out var mine) &&
               theirs.AccountType >= mine.AccountType;
    }
}
