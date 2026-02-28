using Moongate.Server.Data.Scripting;

namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Stores and resolves Lua brain definitions by brain id.
/// </summary>
public interface ILuaBrainRegistry
{
    /// <summary>
    /// Registers or replaces a brain definition.
    /// </summary>
    /// <param name="definition">Brain definition.</param>
    void Register(LuaBrainDefinition definition);

    /// <summary>
    /// Resolves a brain definition by id.
    /// </summary>
    /// <param name="brainId">Brain identifier.</param>
    /// <param name="definition">Resolved definition.</param>
    /// <returns><see langword="true" /> when found.</returns>
    bool TryGet(string brainId, out LuaBrainDefinition? definition);
}
