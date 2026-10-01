using Lua;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services.Internal;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Opens a gump of <c>templates/gumps</c> on the game master, as <c>gump.open</c> would, to try it:
///     <c>gump &lt;id&gt; [name=value ...]</c>; the pairs fill its <c>${name}</c> placeholders.
/// </summary>
public sealed class GumpCommand : ICommandExecutor
{
    private const string UsageText = "gump <id> [name=value ...]";

    private readonly GumpModule _gumps;
    private readonly IGumpTemplateService _templates;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public GumpCommand(
        GumpModule gumps,
        IGumpTemplateService templates,
        IGameLoopService loop,
        ILocalizationService? localization = null
    )
    {
        _gumps = gumps;
        _templates = templates;
        _loop = loop;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        if (context.Session is not { } session)
        {
            context.PrintError("gump works in game only.");

            return;
        }

        if (context.Arguments.Length == 0 || context.Arguments.Skip(1).Any(pair => !pair.Contains('=')))
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", UsageText));

            return;
        }

        var id = context.Arguments[0];

        if (!_templates.Exists(id))
        {
            context.PrintError(_localization.Text(CommandMessages.GumpNotFound, "No gump {0} in templates/gumps.", id));

            return;
        }

        var args = new LuaTable();

        foreach (var pair in context.Arguments.Skip(1))
        {
            var equals = pair.IndexOf('=');
            args[pair[..equals]] = pair[(equals + 1)..];
        }

        var open = new LoopActionWorkItem(() => _gumps.Open(session.CharacterId.Value, id, args));
        await _loop.PostAsync(open, context.CancellationToken);
        await open.Completion;
    }
}
