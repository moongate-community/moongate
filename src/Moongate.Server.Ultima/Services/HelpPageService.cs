using System.Collections.Concurrent;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Events;
using Moongate.Server.Ultima.Data.Help;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Help;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <inheritdoc cref="IHelpPageService" />
public sealed class HelpPageService : IHelpPageService
{
    public const int MaxText = 128;

    public const int KindMessage = 30207;
    public const int AlertMessage = 30216;
    public const int AnswerMessage = 30217;
    public const int WaitingMessage = 30218;

    private const long MillisecondsADay = 86_400_000;

    private readonly ILogger _logger;
    private readonly IDataAccess<HelpPageEntity> _table;
    private readonly IMobileService _mobiles;
    private readonly ISessionService _sessions;
    private readonly ISpeechService _speech;
    private readonly IMoongateEventBus _events;
    private readonly IGameLoopService _loop;
    private readonly HelpConfig _config;
    private readonly TimeProvider _time;
    private readonly ILocalizationService? _localization;

    private readonly Dictionary<Serial, HelpPageEntity> _pages = new();

    // The requests deleted since the last world save, which deletes their rows.
    private readonly ConcurrentDictionary<Serial, byte> _removed = new();

    // When each player last asked, in Unix milliseconds. Kept apart from the pages: the pause outlives the request.
    private readonly Dictionary<Serial, long> _lastAsked = new();

    private uint _next = 1;
    private IDisposable? _logins;

    public IReadOnlyCollection<HelpPageEntity> Pages => _pages.Values;

    public int WaitingCount => _pages.Values.Count(page => page.IsActive);

    public HelpPageService(
        IDataAccess<HelpPageEntity> table,
        IMobileService mobiles,
        ISessionService sessions,
        ISpeechService speech,
        IMoongateEventBus events,
        IGameLoopService loop,
        HelpConfig config,
        TimeProvider time,
        ILocalizationService? localization = null,
        ILogger? logger = null
    )
    {
        _logger = logger ?? Log.ForContext<HelpPageService>();
        _table = table;
        _mobiles = mobiles;
        _sessions = sessions;
        _speech = speech;
        _events = events;
        _loop = loop;
        _config = config;
        _time = time;
        _localization = localization;
    }

    public async Task StartAsync()
    {
        var cutoff = Now() - _config.PageHistoryDays * MillisecondsADay;

        foreach (var page in await _table.GetAllAsync())
        {
            // An answer nobody has read is kept, however old the page is.
            if (!page.IsActive && page.AnswerDelivered && page.ClosedAt < cutoff)
            {
                _removed[page.Id] = 0;

                continue;
            }

            _pages[page.Id] = page;
            _lastAsked[page.Player] = Math.Max(_lastAsked.GetValueOrDefault(page.Player), page.CreatedAt);
        }

        _next = _pages.Count == 0 ? 1 : _pages.Keys.Max(id => id.Value) + 1;

        // The event comes from the login handler's thread; the queue changes on the game loop only.
        _logins = _events.Subscribe<CharacterEnteredWorldEvent>(async (evt, cancellationToken) =>
            {
                var work = new LoopActionWorkItem(() => LoggedIn(evt.Character));
                await _loop.PostAsync(work, cancellationToken);
                await work.Completion;
            }
        );

        _logger.Information("Loaded {Count} help pages", _pages.Count);
    }

    public Task StopAsync()
    {
        _logins?.Dispose();
        _logins = null;

        return Task.CompletedTask;
    }

    public HelpPageCreateResult Create(MobileEntity player, HelpPageKindType kind, string text)
    {
        var clean = Clean(text);

        if (clean.Length == 0)
        {
            return new() { Type = HelpPageCreateResultType.BadText };
        }

        var allowed = CanCreate(player.Id);

        if (allowed.Type != HelpPageCreateResultType.Ok)
        {
            return allowed;
        }

        var now = Now();
        var page = new HelpPageEntity
        {
            Id = new Serial(_next++), Player = player.Id, PlayerName = player.Name,
            AccountId = player.AccountId ?? Serial.Zero, Kind = kind,
            Text = clean, Map = player.Map, X = player.Location.X, Y = player.Location.Y, Z = player.Location.Z,
            Status = HelpPageStatusType.Open, CreatedAt = now
        };
        _pages[page.Id] = page;
        _lastAsked[player.Id] = now;
        Alert(page);

        return new() { Type = HelpPageCreateResultType.Ok, Page = page };
    }

    public HelpPageCreateResult CanCreate(Serial player)
    {
        if (_pages.Values.Any(page => page.IsActive && page.Player == player))
        {
            return new() { Type = HelpPageCreateResultType.AlreadyOpen };
        }

        if (_lastAsked.TryGetValue(player, out var last))
        {
            var leftMilliseconds = last + _config.PageCooldownSeconds * 1000L - Now();

            if (leftMilliseconds > 0)
            {
                return new()
                {
                    Type = HelpPageCreateResultType.Wait, WaitSeconds = (int)Math.Ceiling(leftMilliseconds / 1000.0)
                };
            }
        }

        return new() { Type = HelpPageCreateResultType.Ok };
    }

    public IReadOnlyList<HelpPageEntity> Active()
    {
        return _pages.Values.Where(page => page.IsActive)
            .OrderBy(page => page.CreatedAt)
            .ThenBy(page => page.Id.Value)
            .ToList();
    }

    public HelpPageEntity? Get(Serial id)
    {
        return _pages.GetValueOrDefault(id);
    }

    public bool Take(Serial id, string staffName)
    {
        if (Get(id) is not { IsActive: true } page)
        {
            return false;
        }

        page.Status = HelpPageStatusType.Taken;
        page.TakenBy = staffName;

        return true;
    }

    public bool Answer(Serial id, string staffName, string text)
    {
        var clean = Clean(text);

        if (clean.Length == 0 || Get(id) is not { IsActive: true } page)
        {
            return false;
        }

        page.Status = HelpPageStatusType.Closed;
        page.TakenBy = staffName;
        page.Answer = clean;
        page.ClosedAt = Now();
        page.AnswerDelivered = false;
        Deliver(page);

        return true;
    }

    public bool Close(Serial id, string staffName)
    {
        if (Get(id) is not { IsActive: true } page)
        {
            return false;
        }

        page.Status = HelpPageStatusType.Closed;
        page.TakenBy = staffName;
        page.ClosedAt = Now();
        page.AnswerDelivered = true;

        return true;
    }

    public IReadOnlyCollection<Serial> Capture()
    {
        return _removed.Keys.ToArray();
    }

    public void Committed(IReadOnlyCollection<Serial> serials)
    {
        foreach (var serial in serials)
        {
            _removed.TryRemove(serial, out _);
        }
    }

    // The line without control characters, cut at MaxText without splitting a character of two code units: a lone
    // surrogate cannot be written to the database.
    private static string Clean(string? text)
    {
        var line = new string((text ?? "").Where(letter => !char.IsControl(letter)).ToArray()).Trim();

        if (line.Length <= MaxText)
        {
            return line;
        }

        var length = char.IsHighSurrogate(line[MaxText - 1]) ? MaxText - 1 : MaxText;

        return line[..length].TrimEnd();
    }

    private long Now()
    {
        return _time.GetUtcNow().ToUnixTimeMilliseconds();
    }

    private string KindName(HelpPageKindType kind)
    {
        return _localization.Text(KindMessage + (int)kind, kind.ToString());
    }

    // Tells every game master in the world that a request arrived.
    private void Alert(HelpPageEntity page)
    {
        var text = _localization.Text(
            AlertMessage,
            "{0} asks for help ({1}): {2}",
            page.PlayerName,
            KindName(page.Kind),
            page.Text
        );

        foreach (var session in _sessions.GetAll())
        {
            if (session.AccountType >= AccountType.GameMaster && _mobiles.TryGet(session.CharacterId, out var staff))
            {
                _speech.Tell(staff, text);
            }
        }
    }

    // Tells the player the answer when it is in the world; otherwise the answer waits for its next login.
    private void Deliver(HelpPageEntity page)
    {
        if (!_mobiles.TryGet(page.Player, out var player))
        {
            return;
        }

        // A player with no session to read it is not told: the answer waits for its next login.
        page.AnswerDelivered = _speech.Tell(
            player,
            _localization.Text(AnswerMessage, "Game master {0} answers: {1}", page.TakenBy, page.Answer)
        );
    }

    private void LoggedIn(MobileEntity character)
    {
        foreach (var page in _pages.Values
                     .Where(page => page.Player == character.Id && !page.IsActive && !page.AnswerDelivered)
                     .ToArray())
        {
            Deliver(page);
        }

        if (WaitingCount > 0 &&
            _sessions.TryGetByCharacterId(character.Id, out var session) &&
            session.AccountType >= AccountType.GameMaster)
        {
            _speech.Tell(
                character,
                _localization.Text(WaitingMessage, "Help requests waiting: {0}. Type .pages.", WaitingCount)
            );
        }
    }
}
