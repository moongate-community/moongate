using Moongate.Server.Data.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Stores and validates Lua-authored quest definitions.
/// </summary>
public interface IQuestDefinitionService
{
    /// <summary>
    /// Removes all registered quest definitions.
    /// </summary>
    void Clear();

    /// <summary>
    /// Replaces all registered quest definitions with the provided snapshot.
    /// </summary>
    /// <param name="definitions">Quest definitions to restore.</param>
    void ReplaceAll(IEnumerable<QuestLuaDefinition> definitions);

    /// <summary>
    /// Gets all registered quest definitions as a snapshot list.
    /// </summary>
    /// <returns>All registered quest definitions.</returns>
    IReadOnlyList<QuestLuaDefinition> GetAll();

    /// <summary>
    /// Registers a quest definition emitted by the Lua DSL.
    /// </summary>
    /// <param name="definition">Quest definition table.</param>
    /// <param name="scriptPath">Optional source script path.</param>
    /// <returns><c>true</c> when the definition was registered; otherwise <c>false</c>.</returns>
    bool Register(Table? definition, string? scriptPath = null);

    /// <summary>
    /// Tries to resolve a registered quest definition by id.
    /// </summary>
    /// <param name="questId">Quest id.</param>
    /// <param name="definition">Resolved definition when present.</param>
    /// <returns><c>true</c> when found; otherwise <c>false</c>.</returns>
    bool TryGet(string questId, out QuestLuaDefinition? definition);
}
