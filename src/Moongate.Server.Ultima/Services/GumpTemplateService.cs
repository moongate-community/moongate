using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Data.Templates.Gumps;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Gumps;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Renders the gumps of <c>templates/gumps</c> and opens them through <see cref="IGumpService" />, which checks the
///     answers.
/// </summary>
public sealed class GumpTemplateService : IGumpTemplateService
{
    private readonly ILogger _logger = Log.ForContext<GumpTemplateService>();
    private readonly IGumpService _gumps;
    private readonly IDataLoaderService _data;
    private readonly IGameLoopService _loop;
    private readonly ISessionService _sessions;
    private readonly ILocalizationService? _localization;

    public GumpTemplateService(
        IGumpService gumps,
        IDataLoaderService data,
        IGameLoopService loop,
        ISessionService sessions,
        ILocalizationService? localization = null
    )
    {
        _gumps = gumps;
        _data = data;
        _loop = loop;
        _sessions = sessions;
        _localization = localization;
    }

    public bool Exists(string id)
    {
        return Find(id) is not null;
    }

    public bool Open(
        GameSession session,
        string id,
        IReadOnlyDictionary<string, string> args,
        Action<GameSession, GumpTemplateAnswer> onAnswer,
        Action<GameSession, GumpCloseReasonType>? onClosed = null
    )
    {
        if (Find(id) is not { } template)
        {
            _logger.Warning("No gump {Gump} in templates/gumps", id);

            return false;
        }

        // A session closed before this ran would never answer nor be told of a close.
        if (!_sessions.TryGet(session.SessionId, out var live) || !ReferenceEquals(live, session))
        {
            return false;
        }

        var rendered = GumpXmlRenderer.Render(template, args, _localization);
        _gumps.Open(
            session,
            new()
            {
                Id = id, Layout = rendered.Layout, X = rendered.X, Y = rendered.Y,
                OnResponse = (answered, response) => onAnswer(
                    answered,
                    new() { Response = response, Click = rendered.Clicks.GetValueOrDefault(response.ButtonId) }
                ),
                OnClosed = onClosed
            }
        );

        return true;
    }

    public async Task<string?> AskAsync(
        GameSession session,
        string id,
        IReadOnlyDictionary<string, string> args,
        CancellationToken cancellationToken = default
    )
    {
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var open = new LoopActionWorkItem(
            () =>
            {
                if (!Open(
                        session,
                        id,
                        args,
                        (_, answer) => completion.TrySetResult(answer.Click),
                        (_, _) => completion.TrySetResult(null)
                    ))
                {
                    completion.TrySetResult(null);
                }
            }
        );
        await _loop.PostAsync(open, cancellationToken);
        await open.Completion;

        await using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));

        return await completion.Task;
    }

    private GumpTemplate? Find(string id)
    {
        return _data.GetEntities<GumpTemplate>().FirstOrDefault(gump => gump.Id == id);
    }
}
