using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Books;
using Moongate.Server.Ultima.Services.Internal;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Gives the invoking character a document from a loaded book template, with creation-time values.
/// </summary>
public sealed class BookCommand : ICommandExecutor
{
    private const string UsageText = "book <template> [name=value ...]";

    private readonly IBookDocumentService _books;
    private readonly IBookTemplateService _templates;
    private readonly IMobileService _mobiles;
    private readonly ISessionService _sessions;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public BookCommand(
        IBookDocumentService books, IBookTemplateService templates, IMobileService mobiles,
        ISessionService sessions, IGameLoopService loop, ILocalizationService? localization = null
    )
    {
        _books = books;
        _templates = templates;
        _mobiles = mobiles;
        _sessions = sessions;
        _loop = loop;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Source != CommandSourceType.InGame || context.Session is not { } session)
        {
            context.PrintError("book works in game only.");
            return;
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (context.Arguments.Length == 0 || !TryValues(context.Arguments, values))
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", UsageText));
            return;
        }

        var id = context.Arguments[0];
        if (!_templates.TryGet(id, out _))
        {
            context.PrintError(
                _localization.Text(CommandMessages.UnknownBookTemplate, "No document {0} in templates/books.", id)
            );
            return;
        }

        var characterId = session.CharacterId;
        var made = false;
        var work = new LoopActionWorkItem(() =>
            {
                if (!context.CancellationToken.IsCancellationRequested && characterId.IsValid &&
                    session.CharacterId == characterId &&
                    session.NetworkSession.Client is { IsConnected: true } &&
                    _sessions.TryGetByCharacterId(characterId, out var current) && ReferenceEquals(current, session) &&
                    _mobiles.TryGet(characterId, out var character))
                {
                    made = _books.Give(character, id, values) is not null;
                }
            }
        );
        await _loop.PostAsync(work, context.CancellationToken);
        await work.Completion;

        if (made)
        {
            context.Print(_localization.Text(CommandMessages.BookCreated, "Document {0} is in your backpack.", id));
        }
        else
        {
            context.PrintError(
                _localization.Text(
                    CommandMessages.BookNotCreated,
                    "Document {0} could not be created. Check its values and backpack availability.",
                    id
                )
            );
        }
    }

    private static bool TryValues(string[] arguments, Dictionary<string, object?> values)
    {
        foreach (var pair in arguments.Skip(1))
        {
            var equals = pair.IndexOf('=');
            if (equals < 1 || !values.TryAdd(pair[..equals], pair[(equals + 1)..]))
            {
                return false;
            }
        }

        return true;
    }
}
