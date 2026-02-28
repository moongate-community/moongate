using System.Collections.Concurrent;
using Moongate.Server.Data.Scripting;
using Moongate.Server.Interfaces.Services.Scripting;

namespace Moongate.Server.Services.Scripting;

/// <summary>
/// In-memory registry of Lua brain definitions keyed by brain id.
/// </summary>
public sealed class LuaBrainRegistry : ILuaBrainRegistry
{
    private readonly ConcurrentDictionary<string, LuaBrainDefinition> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Register(LuaBrainDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (string.IsNullOrWhiteSpace(definition.BrainId))
        {
            throw new ArgumentException("BrainId is required.", nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(definition.ScriptPath))
        {
            throw new ArgumentException("ScriptPath is required.", nameof(definition));
        }

        _definitions[definition.BrainId.Trim()] = definition;
    }

    /// <inheritdoc />
    public bool TryGet(string brainId, out LuaBrainDefinition? definition)
    {
        definition = null;

        if (string.IsNullOrWhiteSpace(brainId))
        {
            return false;
        }

        if (_definitions.TryGetValue(brainId.Trim(), out var resolved))
        {
            definition = resolved;

            return true;
        }

        return false;
    }
}
