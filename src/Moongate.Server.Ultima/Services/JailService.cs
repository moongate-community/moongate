using Moongate.Server.Ultima.Interfaces.Items;
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
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Books;
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
    public const int JailedForMessage = 30148;
    public const int NoteMessage = 30143;
    public const int NoteReasonMessage = 30149;

    /// <summary>
    ///     The longest reason kept with a sentence; what is typed beyond it is cut.
    /// </summary>
    public const int MaxReasonLength = 100;

    private const long MillisecondsADay = 86_400_000;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);

    private readonly ILogger _logger;
    private readonly IDataLoaderService _data;
    private readonly IDataAccess<JailSentenceEntity> _table;
    private readonly IDataAccess<MobileEntity> _characters;
    private readonly IDataAccess<AccountEntity> _accounts;
    private readonly IMobileService _mobiles;
    private readonly ISessionService _sessions;
    private readonly ITeleportService _teleports;
    private readonly ISpeechService _speech;
    private readonly ITimerService _timers;
    private readonly JailConfig _config;
    private readonly ItemsConfig _itemsConfig;
    private readonly IItemService _items;
    private readonly IItemHandlingService _handling;
    private readonly IBookDocumentService _books;
    private readonly IWorldViewService _view;
    private readonly TimeProvider _time;
    private readonly ILocalizationService? _localization;

    private readonly Dictionary<Serial, JailSentenceEntity> _sentences = new();

    // The sentences that ended since the last world save, which deletes their rows.
    private readonly ConcurrentDictionary<Serial, byte> _ended = new();

    // Who FindAsync gave: the only serials that can be jailed while they are not in the world.
    private readonly ConcurrentDictionary<Serial, JailCandidate> _found = new();

    private JailFile? _file;
    private string? _timerId;

    public bool IsEnabled => _file is not null;

    public IReadOnlyList<JailCell> Cells => _file?.Cell ?? [];

    public MapType? Map => _file?.Map;

    public IReadOnlyCollection<JailSentenceEntity> Sentences => _sentences.Values;

    public int MaxDays => _config.MaxDays;

    private readonly IInventoryMutationGuard? _inventory;

    public JailService(
        IDataLoaderService data,
        IDataAccess<JailSentenceEntity> table,
        IDataAccess<MobileEntity> characters,
        IDataAccess<AccountEntity> accounts,
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
        IBookDocumentService books,
        ILocalizationService? localization = null,
        ILogger? logger = null,
        IInventoryMutationGuard? inventory = null
    )
    {
        _inventory = inventory;
        _logger = logger ?? Log.ForContext<JailService>();
        _data = data;
        _table = table;
        _characters = characters;
        _accounts = accounts;
        _mobiles = mobiles;
        _sessions = sessions;
        _teleports = teleports;
        _speech = speech;
        _timers = timers;
        _config = config;
        _itemsConfig = itemsConfig;
        _items = items;
        _handling = handling;
        _books = books;
        _view = view;
        _time = time;
        _localization = localization;
    }

    public async Task StartAsync()
    {
        var files = _data.GetEntities<JailFile>();
        _file = files.Count > 0 ? files[0] : null;

        foreach (var sentence in await _table.GetAllAsync())
        {
            // A row saved before the reason existed has none.
            sentence.Reason ??= "";
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

    public async Task<IReadOnlyList<JailCandidate>> FindAsync(string name, CancellationToken cancellationToken = default)
    {
        var wanted = name.Trim().ToLowerInvariant();

        if (wanted.Length == 0)
        {
            return [];
        }

        // The database lowers the name: this ToLower becomes its lower(), where a culture means nothing.
#pragma warning disable CA1311
        var characters = await _characters.QueryAsync(
            mobile => mobile.AccountId != null && mobile.DeletionRequestedAt == null && mobile.Name.ToLower() == wanted,
            cancellationToken
        );
#pragma warning restore CA1311
        var found = new List<JailCandidate>();

        foreach (var character in characters.OrderBy(character => character.Id.Value))
        {
            if (await _accounts.GetByIdAsync(character.AccountId.Value, cancellationToken) is not { } account)
            {
                continue;
            }

            var candidate = new JailCandidate
            {
                Id = character.Id, Name = character.Name, Account = account.Username, AccountType = account.AccountType
            };
            _found[candidate.Id] = candidate;
            found.Add(candidate);
        }

        return found;
    }

    public JailResultType Jail(MobileEntity prisoner, int cell, int days, MobileEntity by, string? reason = null)
    {
        if (_inventory?.AllowsOwner(prisoner.Id) == false)
        {
            return JailResultType.Refused;
        }

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

        // A player whose login is still being sent: a teleport now would reach its client before it knows where it
        // stands. Its sentence waits, and the next check takes it once it has entered.
        if (!prisoner.IsNpc && !_view.HasEntered(prisoner.Id))
        {
            return Wait(prisoner.Id, prisoner.Name, cell, days, by, reason);
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
        else if (sentence.JailedAt == 0)
        {
            // One that waited for this login and never began: it has no place to go back to yet.
            (sentence.ReturnMap, sentence.ReturnX, sentence.ReturnY, sentence.ReturnZ) =
                (map, location.X, location.Y, location.Z);
        }

        sentence.Pending = false;
        sentence.Name = prisoner.Name;
        sentence.IsPlayer = !prisoner.IsNpc;
        sentence.Cell = cell;
        sentence.Days = days;
        sentence.JailedAt = now;
        sentence.ReleaseAt = now + days * MillisecondsADay;
        sentence.JailedBy = by.Name;
        sentence.Reason = Clean(reason);
        sentence.Pardoned = false;

        Announce(sentence, prisoner);

        return JailResultType.Ok;
    }

    public JailResultType JailOffline(Serial prisoner, int cell, int days, MobileEntity by, string? reason = null)
    {
        // It logged in after it was found.
        if (_mobiles.TryGet(prisoner, out var online))
        {
            return Jail(online, cell, days, by, reason);
        }

        if (_file is not { } file)
        {
            return JailResultType.Disabled;
        }

        if (days < 1 || days > _config.MaxDays)
        {
            return JailResultType.BadDays;
        }

        if (file.Cell.All(candidate => candidate.Number != cell))
        {
            return JailResultType.NoSuchCell;
        }

        // Only who the search by name gave: a script cannot jail any serial it likes.
        if (!_found.TryGetValue(prisoner, out var found))
        {
            return JailResultType.NotInWorld;
        }

        if (prisoner == by.Id ||
            (_sessions.TryGetByCharacterId(by.Id, out var mine) && found.AccountType >= mine.AccountType))
        {
            return JailResultType.Refused;
        }

        if (GetOccupant(cell) is { } occupant && occupant.Id != prisoner)
        {
            return JailResultType.CellOccupied;
        }

        return Wait(prisoner, found.Name, cell, days, by, reason);
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

        if (sentence.Pending && sentence.JailedAt == 0)
        {
            // Nobody was moved and nothing was taken: there is nothing to give back.
            End(sentence);
            _logger.Information(
                "The sentence of {Name:l} ({Serial:l}) for cell {Cell} is dropped before it began",
                sentence.Name,
                sentence.Id,
                sentence.Cell
            );

            return true;
        }

        // One that had run and was made to wait: its prisoner still stands in a cell, and leaves it as any other.
        sentence.Pending = false;
        sentence.Pardoned = true;
        sentence.ReleaseAt = Now();
        Check();

        return true;
    }

    // A timer callback that throws closes the timer wheel: one bad prisoner must not stop the server.
    public void Check()
    {
        foreach (var sentence in _sentences.Values.Where(sentence => sentence.Pending).ToArray())
        {
            try
            {
                if (_inventory?.AllowsOwner(sentence.Id) != false) Begin(sentence);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "The jailing of {Prisoner} at its login failed", sentence.Id);
            }
        }

        var now = Now();

        foreach (var sentence in _sentences.Values.Where(sentence => sentence.IsOver(now)).ToArray())
        {
            try
            {
                if (_inventory?.AllowsOwner(sentence.Id) == false) continue;
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
                    _logger.Information(
                        "The sentence of {Name:l} ({Serial:l}) in cell {Cell} is dropped: it is no longer in the world",
                        sentence.Name,
                        sentence.Id,
                        sentence.Cell
                    );
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "The release of {Prisoner} from jail failed", sentence.Id);
            }
        }
    }

    // The sentence is kept with its cell and nobody is moved: the check starts it when its player has entered the world.
    private JailResultType Wait(Serial prisoner, string name, int cell, int days, MobileEntity by, string? reason)
    {
        if (!_sentences.TryGetValue(prisoner, out var sentence))
        {
            // No place to go back to yet: it is taken where the player logs in.
            sentence = new JailSentenceEntity { Id = prisoner };
            _sentences[prisoner] = sentence;
            _ended.TryRemove(prisoner, out _);
        }

        sentence.Name = name;
        sentence.IsPlayer = true;
        sentence.Cell = cell;
        sentence.Days = days;
        sentence.JailedBy = by.Name;
        sentence.Reason = Clean(reason);
        sentence.Pardoned = false;
        sentence.Pending = true;
        sentence.ReleaseAt = 0;

        if (sentence.Reason.Length == 0)
        {
            _logger.Information(
                "{Name:l} ({Serial:l}) will be jailed in cell {Cell} for {Days} days at its next login, by {By:l}",
                sentence.Name,
                sentence.Id,
                cell,
                days,
                sentence.JailedBy
            );
        }
        else
        {
            _logger.Information(
                "{Name:l} ({Serial:l}) will be jailed in cell {Cell} for {Days} days at its next login, by {By:l}: {Reason:l}",
                sentence.Name,
                sentence.Id,
                cell,
                days,
                sentence.JailedBy,
                sentence.Reason
            );
        }

        return JailResultType.Pending;
    }

    // The prisoner is told its days, and the console who went where.
    private void Announce(JailSentenceEntity sentence, MobileEntity prisoner)
    {
        if (sentence.Reason.Length == 0)
        {
            _speech.Tell(prisoner, _localization.Text(JailedMessage, "You have been jailed for {0} days.", sentence.Days));
            _logger.Information(
                "{Name:l} ({Serial:l}) is jailed in cell {Cell} for {Days} days by {By:l}",
                sentence.Name,
                sentence.Id,
                sentence.Cell,
                sentence.Days,
                sentence.JailedBy
            );
        }
        else
        {
            _speech.Tell(
                prisoner,
                _localization.Text(
                    JailedForMessage,
                    "You have been jailed for {0} days: {1}",
                    sentence.Days,
                    sentence.Reason
                )
            );
            _logger.Information(
                "{Name:l} ({Serial:l}) is jailed in cell {Cell} for {Days} days by {By:l}: {Reason:l}",
                sentence.Name,
                sentence.Id,
                sentence.Cell,
                sentence.Days,
                sentence.JailedBy,
                sentence.Reason
            );
        }
    }

    // A sentence given while its prisoner was offline starts when it is back and its login is over.
    private void Begin(JailSentenceEntity sentence)
    {
        if (!_mobiles.TryGet(sentence.Id, out var prisoner) || !_view.HasEntered(prisoner.Id))
        {
            return;
        }

        // Where it stands now, read before the teleport moves it.
        var (map, location) = (prisoner.Map, prisoner.Location);

        if (_file is not { } file ||
            file.Cell.FirstOrDefault(candidate => candidate.Number == sentence.Cell) is not { } cell ||
            !_teleports.Teleport(prisoner, file.Map, cell.Location))
        {
            _logger.Warning(
                "{Name:l} ({Serial:l}) waits for cell {Cell}, which cannot be reached",
                sentence.Name,
                sentence.Id,
                sentence.Cell
            );

            return;
        }

        // A sentence that had run before keeps the place of its first arrest.
        if (sentence.JailedAt == 0)
        {
            (sentence.ReturnMap, sentence.ReturnX, sentence.ReturnY, sentence.ReturnZ) =
                (map, location.X, location.Y, location.Z);
        }

        var now = Now();
        sentence.Pending = false;
        sentence.JailedAt = now;
        sentence.ReleaseAt = now + sentence.Days * MillisecondsADay;
        Announce(sentence, prisoner);
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
            _logger.Information(
                "{Name:l} ({Serial:l}) is released early from cell {Cell}",
                sentence.Name,
                sentence.Id,
                sentence.Cell
            );
            _speech.Tell(prisoner, _localization.Text(PardonedMessage, "You have been released from jail."));

            return;
        }

        _logger.Information(
            "{Name:l} ({Serial:l}) is released from cell {Cell} after {Days} days, with a fine of {Fine} gold",
            sentence.Name,
            sentence.Id,
            sentence.Cell,
            sentence.Days,
            fine
        );

        GiveNote(sentence, prisoner, fine);
        _speech.Tell(
            prisoner,
            fine > 0
                ? _localization.Text(
                    ReleasedFinedMessage,
                    "You have served your sentence. A fine of {0} gold was taken.",
                    fine
                )
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
        var reasonLine = string.IsNullOrEmpty(sentence.Reason)
            ? ""
            : " " + _localization.Text(NoteReasonMessage, "Reason: {0}", sentence.Reason);
        var values = new Dictionary<string, object?>
        {
            ["days"] = sentence.Days,
            ["cell"] = sentence.Cell,
            ["jailed_at"] = Date(sentence.JailedAt),
            ["released_at"] = Date(sentence.ReleaseAt),
            ["fine"] = fine,
            ["jailed_by"] = sentence.JailedBy,
            ["reason_line"] = reasonLine
        };

        if (_books.Give(prisoner, NoteTemplate, values, sentence.Name) is not { } note)
        {
            // A player always has a backpack: the template is missing or the reserved serials ran out.
            if (sentence.IsPlayer)
            {
                _logger.Warning(
                    "{Prisoner} left the jail without its release note: {Template} could not be made",
                    sentence.Id,
                    NoteTemplate
                );
            }

            return;
        }

        note.SetProp("jail.cell", sentence.Cell);
        note.SetProp("jail.days", sentence.Days);
        note.SetProp("jail.fine", fine);
    }

    // One line of plain text, no longer than the limit: what is typed goes on a note, which reads markup, into a
    // message and into the log.
    private static string Clean(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "";
        }

        var plain = reason.Replace('<', ' ').Replace('>', ' ');
        var line = string.Join(' ', plain.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        line = new(line.Where(letter => !char.IsControl(letter)).ToArray());

        if (line.Length <= MaxReasonLength)
        {
            return line;
        }

        // A character of two code units is not cut in half.
        var length = char.IsHighSurrogate(line[MaxReasonLength - 1]) ? MaxReasonLength - 1 : MaxReasonLength;

        return line[..length].TrimEnd();
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
