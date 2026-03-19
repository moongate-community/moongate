using Moongate.Server.Data.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Stores and validates Lua-authored scheduled event definitions.
/// </summary>
public interface IScheduledEventDefinitionService
{
    bool Register(string eventId, Table? definition, string? scriptPath = null);

    bool TryGet(string eventId, out ScheduledEventDefinition? definition);
}
