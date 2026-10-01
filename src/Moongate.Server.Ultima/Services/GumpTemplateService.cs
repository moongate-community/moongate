using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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

    public bool TryGet(string id, [NotNullWhen(true)] out GumpTemplate? template)
    {
        template = Find(id);

        return template is not null;
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

        return Open(session, template, args, onAnswer, onClosed);
    }

    public bool Open(
        GameSession session,
        GumpTemplate template,
        IReadOnlyDictionary<string, string> args,
        Action<GameSession, GumpTemplateAnswer> onAnswer,
        Action<GameSession, GumpCloseReasonType>? onClosed = null
    )
    {
        var id = template.Id;

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
                    new()
                    {
                        Response = response, Click = rendered.Clicks.GetValueOrDefault(response.ButtonId),
                        Open = rendered.Opens.GetValueOrDefault(response.ButtonId), Bound = Bound(rendered.Binds, response)
                    }
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
        var current = id;

        // An open button goes on to its gump, with the bound values added to the arguments.
        void Ask(string gump, IReadOnlyDictionary<string, string> values)
        {
            current = gump;
            var opened = Open(
                session,
                gump,
                values,
                (_, answer) =>
                {
                    if (answer.Open is { } next)
                    {
                        var merged = new Dictionary<string, string>(values, StringComparer.Ordinal);

                        foreach (var (name, value) in answer.Bound)
                        {
                            merged[name] = Format(value);
                        }

                        Ask(next, merged);

                        return;
                    }

                    completion.TrySetResult(answer.Click);
                },
                (_, _) => completion.TrySetResult(null)
            );

            if (!opened)
            {
                completion.TrySetResult(null);
            }
        }

        var open = new LoopActionWorkItem(() => Ask(id, args));
        await _loop.PostAsync(open, cancellationToken);
        await open.Completion;

        await using var registration = cancellationToken.Register(
            () =>
            {
                if (completion.TrySetCanceled(cancellationToken))
                {
                    _loop.TryPost(new LoopActionWorkItem(() => _gumps.Close(session, current)));
                }
            }
        );

        return await completion.Task;
    }

    /// <summary>
    ///     Formats a bound value as a placeholder takes it: a bool as <c>true</c> or <c>false</c>, a number in the
    ///     invariant culture.
    /// </summary>
    public static string Format(object value)
    {
        return value switch
        {
            bool flag => flag ? "true" : "false",
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static Dictionary<string, object> Bound(IReadOnlyList<GumpBind> binds, GumpResponse response)
    {
        var bound = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var bind in binds)
        {
            switch (bind.Kind)
            {
                case GumpBindType.Text:
                    bound[bind.Name] = response.Texts.GetValueOrDefault(bind.Id, string.Empty);

                    break;
                case GumpBindType.Checkbox:
                    bound[bind.Name] = response.Switches.Contains(bind.Id);

                    break;
                case GumpBindType.Radio when response.Switches.Contains(bind.Id):
                    bound[bind.Name] = (long)bind.Id;

                    break;
            }
        }

        return bound;
    }

    private GumpTemplate? Find(string id)
    {
        return _data.GetEntities<GumpTemplate>().FirstOrDefault(gump => gump.Id == id);
    }
}
