using System.Collections.Concurrent;
using System.Globalization;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Jail;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Types.Jail;
using Moongate.Ultima.Types;
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
    public const string NoteTemplate = "jail_release_note";
    public const string NoteTextProp = "jail.text";
    public const int JailedMessage = 30139;
    public const int ReleasedFinedMessage = 30140;
    public const int ReleasedMessage = 30141;
    public const int PardonedMessage = 30142;
    public const int NoteMessage = 30143;

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
    private readonly ItemsConfig _itemsConfig;
    private readonly IItemService _items;
    private readonly IItemHandlingService _handling;
    private readonly IWorldViewService _view;
    private readonly TimeProvider _time;
    private readonly ILocalizationService? _localization;

    private readonly Dictionary<Serial, JailSentenceEntity> _sentences = new();

    // The sentences that ended since the last world save, which deletes their rows.
    private readonly ConcurrentDictionary<Serial, byte> _ended = new();

    private JailFile? _file;
    private string? _timerId;

    public bool IsEnabled => _file is not null;

    public IReadOnlyList<JailCell> Cells => _file?.Cell ?? [];

    public MapType? Map => _file?.Map;

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
        ItemsConfig itemsConfig,
        IItemService items,
        IItemHandlingService handling,
        IWorldViewService view,
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
        _itemsConfig = itemsConfig;
        _items = items;
        _handling = handling;
        _view = view;
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

    public bool Pardon(Serial prisoner)
    {
        if (!_sentences.TryGetValue(prisoner, out var sentence))
        {
            return false;
        }

        sentence.Pardoned = true;
        sentence.ReleaseAt = Now();
        Check();

        return true;
    }

    // A timer callback that throws closes the timer wheel: one bad prisoner must not stop the server.
    public void Check()
    {
        var now = Now();

        foreach (var sentence in _sentences.Values.Where(sentence => sentence.IsOver(now)).ToArray())
        {
            try
            {
                if (_mobiles.TryGet(sentence.Id, out var prisoner))
                {
                    // A player whose login is still being sent: a teleport now would reach its client before it
                    // knows where it stands. The next check finds it entered.
                    if (sentence.IsPlayer && !_view.HasEntered(prisoner.Id))
                    {
                        continue;
                    }

                    // Gold on the cursor cannot be taken: the prisoner waits in its cell until it drops what it holds.
                    if (OwesAFine(sentence) && HoldsSomething(prisoner))
                    {
                        continue;
                    }

                    // Ended before anything is taken or given: a release that fails is not done twice.
                    End(sentence);
                    Release(sentence, prisoner);
                }
                else if (!sentence.IsPlayer)
                {
                    // An NPC that is no longer in the world was removed; a player is offline and is released at its login.
                    End(sentence);
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "The release of {Prisoner} from jail failed", sentence.Id);
            }
        }
    }

    private bool OwesAFine(JailSentenceEntity sentence)
    {
        return !sentence.Pardoned && _config.FineGold > 0;
    }

    private bool HoldsSomething(MobileEntity prisoner)
    {
        return _sessions.TryGetByCharacterId(prisoner.Id, out var session) && session.Get(ItemSessionKeys.Held) is not null;
    }

    private void End(JailSentenceEntity sentence)
    {
        _sentences.Remove(sentence.Id);
        _ended[sentence.Id] = 0;
    }

    private void Release(JailSentenceEntity sentence, MobileEntity prisoner)
    {
        var fine = sentence.Pardoned ? 0 : TakeGold(prisoner, _config.FineGold);
        var back = new Point3D(sentence.ReturnX, sentence.ReturnY, sentence.ReturnZ);

        // The map it was arrested on may no longer be loaded.
        if (!_teleports.Teleport(prisoner, sentence.ReturnMap, back) &&
            (_file is not { } file || !_teleports.Teleport(prisoner, file.Map, file.Release)))
        {
            _logger.Warning("{Prisoner} is released from jail but could not be moved out of its cell", sentence.Id);
        }

        if (sentence.Pardoned)
        {
            _speech.Tell(prisoner, _localization.Text(PardonedMessage, "You have been released from jail."));

            return;
        }

        GiveNote(sentence, prisoner, fine);
        _speech.Tell(
            prisoner,
            fine > 0
                ? _localization.Text(ReleasedFinedMessage, "You have served your sentence. A fine of {0} gold was taken.", fine)
                : _localization.Text(ReleasedMessage, "You have served your sentence.")
        );
    }

    // Up to amount coins, from what the prisoner carries first and then from its bank box; what was taken.
    private int TakeGold(MobileEntity prisoner, int amount)
    {
        var left = amount;
        var piles = _items.GetOwnedBy(prisoner.Id)
                          .Where(item => item.TemplateId == _itemsConfig.GoldTemplate)
                          .OrderBy(pile => _items.GetWornRoot(pile)?.Layer == LayerType.Bank)
                          .ToArray();

        foreach (var pile in piles)
        {
            if (left <= 0)
            {
                break;
            }

            var taken = Math.Min(left, pile.Amount);

            if (_handling.Consume(pile, taken))
            {
                left -= taken;
            }
        }

        return amount - left;
    }

    // An NPC without a backpack gets no note.
    private void GiveNote(JailSentenceEntity sentence, MobileEntity prisoner, int fine)
    {
        if (_handling.Give(prisoner, NoteTemplate) is not { } note)
        {
            // A player always has a backpack: the template is missing or the reserved serials ran out.
            if (sentence.IsPlayer)
            {
                _logger.Warning("{Prisoner} left the jail without its release note: {Template} could not be made", sentence.Id, NoteTemplate);
            }

            return;
        }

        note.SetProp(
            NoteTextProp,
            _localization.Text(
                NoteMessage,
                "{0} served {1} days in cell {2}, from {3} to {4}, and paid a fine of {5} gold. Jailed by {6}.",
                sentence.Name,
                sentence.Days,
                sentence.Cell,
                Date(sentence.JailedAt),
                Date(sentence.ReleaseAt),
                fine,
                sentence.JailedBy
            )
        );
        note.SetProp("jail.cell", sentence.Cell);
        note.SetProp("jail.days", sentence.Days);
        note.SetProp("jail.fine", fine);
    }

    private static string Date(long milliseconds)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
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
