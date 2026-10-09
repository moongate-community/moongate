using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Data.Localization;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;

namespace Moongate.Server.Ultima.Commands;

/// <summary>
///     Shows the seasonal events,
///     <c>
///         event list
///     </c>
///     , and forces one on, off, or back to its dates:
///     <c>
///         event on|off|auto &lt;id&gt;
///     </c>
///     .
/// </summary>
public sealed class EventCommand : ICommandExecutor
{
    private const string UsageText = "event list | on <id> | off <id> | auto <id>";
    private const string List = "list";

    private readonly ISeasonalEventService _events;
    private readonly IGameLoopService _loop;
    private readonly ILocalizationService? _localization;

    public EventCommand(ISeasonalEventService events, IGameLoopService loop, ILocalizationService? localization = null)
    {
        _events = events;
        _loop = loop;
        _localization = localization;
    }

    public async Task ExecuteAsync(CommandContext context)
    {
        var arguments = context.Arguments;

        if (arguments.Length == 0 || arguments.Length == 1 && IsWord(arguments[0], List))
        {
            PrintList(context);

            return;
        }

        var mode = arguments[0].ToLowerInvariant();

        if (arguments.Length != 2 || mode is not ("on" or "off" or "auto"))
        {
            context.PrintError(_localization.Text(CommandMessages.Usage, "Usage: {0}", UsageText));

            return;
        }

        var id = arguments[1];

        if (_events.Get(id) is null)
        {
            context.PrintError(
                _localization.Text(
                    CommandMessages.EventUnknown,
                    "No event is called {0}. Events: {1}",
                    id,
                    string.Join(", ", _events.Events.Select(item => item.Id))
                )
            );

            return;
        }

        // The hooks of an event are Lua, which belongs to the game loop; commands run off it.
        var change = new LoopActionWorkItem(() => _events.SetMode(id, mode));
        await _loop.PostAsync(change, context.CancellationToken);
        await change.Completion;
        context.Print(_localization.Text(CommandMessages.EventSwitched, "Event {0} is now {1}.", id, mode));
    }

    private static bool IsWord(string text, string word)
    {
        return string.Equals(text, word, StringComparison.OrdinalIgnoreCase);
    }

    private void PrintList(CommandContext context)
    {
        if (_events.Events.Count == 0)
        {
            context.Print(_localization.Text(CommandMessages.EventNone, "The schedule has no events."));

            return;
        }

        foreach (var item in _events.Events)
        {
            context.Print(
                _localization.Text(
                    CommandMessages.EventListLine,
                    "{0}: {1}, {2} to {3}, mode {4}, {5}",
                    item.Id,
                    item.Name,
                    item.From,
                    item.To,
                    item.Mode,
                    item.Active ? "active" : "inactive"
                )
            );
        }
    }
}
