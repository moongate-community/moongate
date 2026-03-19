using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Modules;

[ScriptModule("help_tickets", "Provides help ticket submission APIs for scripts.")]
public sealed class HelpTicketsModule
{
    private readonly IHelpTicketService _helpTicketService;

    public HelpTicketsModule(IHelpTicketService helpTicketService)
    {
        _helpTicketService = helpTicketService;
    }

    [ScriptFunction("submit", "Creates a persisted help ticket for a session and category.")]
    public bool Submit(long sessionId, string category, string message)
    {
        if (sessionId <= 0 ||
            string.IsNullOrWhiteSpace(category) ||
            string.IsNullOrWhiteSpace(message) ||
            !Enum.TryParse<HelpTicketCategory>(category.Trim(), true, out var parsedCategory))
        {
            return false;
        }

        return _helpTicketService.CreateTicketAsync(sessionId, parsedCategory, message.Trim()).GetAwaiter().GetResult()
               is not null;
    }
}
