using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Types.Items;

namespace Moongate.Tests.Support;

/// <summary>Records the hook calls a subscriber makes, without running any Lua.</summary>
public sealed class RecordingItemScriptRuntime : IItemScriptRuntime
{
    public List<(ItemScriptHookType Hook, ItemScriptContext Context)> Calls { get; } = [];

    public bool HasHook(string scriptId, ItemScriptHookType hook)
        => true;

    public bool Invoke(ItemScriptHookType hook, ItemScriptContext context)
    {
        Calls.Add((hook, context));

        return true;
    }
}
