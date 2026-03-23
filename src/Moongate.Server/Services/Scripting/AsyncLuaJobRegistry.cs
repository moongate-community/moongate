using Moongate.Server.Interfaces.Services.Scripting;

namespace Moongate.Server.Services.Scripting;

public sealed class AsyncLuaJobRegistry : IAsyncLuaJobRegistry
{
    private readonly Dictionary<string, IAsyncLuaJobHandler> _handlers = new(StringComparer.Ordinal);

    public bool TryRegister(IAsyncLuaJobHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (string.IsNullOrWhiteSpace(handler.Name))
        {
            return false;
        }

        return _handlers.TryAdd(handler.Name.Trim(), handler);
    }

    public bool TryResolve(string jobName, out IAsyncLuaJobHandler? handler)
    {
        if (string.IsNullOrWhiteSpace(jobName))
        {
            handler = null;

            return false;
        }

        return _handlers.TryGetValue(jobName.Trim(), out handler);
    }
}
