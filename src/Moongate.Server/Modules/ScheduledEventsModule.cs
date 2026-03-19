using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("scheduled_events", "Provides scheduled event registration helpers for scripts.")]

/// <summary>
/// Exposes scheduled event definition registration to Lua scripts.
/// </summary>
public sealed class ScheduledEventsModule
{
    private readonly IScheduledEventDefinitionService _scheduledEventDefinitionService;

    public ScheduledEventsModule(IScheduledEventDefinitionService scheduledEventDefinitionService)
    {
        _scheduledEventDefinitionService = scheduledEventDefinitionService;
    }

    [ScriptFunction("register", "Registers a Lua-authored scheduled event definition.")]
    public bool Register(string eventId, Table? definition)
        => _scheduledEventDefinitionService.Register(eventId, definition);
}
