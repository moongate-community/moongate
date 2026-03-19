using Moongate.Server.Data.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Stores and validates authored dialogue definitions registered from Lua.
/// </summary>
public interface IDialogueDefinitionService
{
    /// <summary>
    /// Registers a conversation definition coming from Lua.
    /// </summary>
    bool Register(string conversationId, Table? definition, string? scriptPath = null);

    /// <summary>
    /// Tries to resolve a previously registered conversation definition.
    /// </summary>
    bool TryGet(string conversationId, out DialogueDefinition? definition);
}
