using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("async_job", "Provides named background job helpers for scripts.")]
public sealed class AsyncJobModule
{
    private readonly IAsyncLuaJobService _asyncLuaJobService;

    public AsyncJobModule(IAsyncLuaJobService asyncLuaJobService)
    {
        _asyncLuaJobService = asyncLuaJobService;
    }

    [ScriptFunction("run", "Runs a named background job and reports completion through Lua callbacks.")]
    public bool Run(string jobName, string requestId, Table? payload = null)
        => _asyncLuaJobService.Run(jobName, requestId, payload);

    [ScriptFunction("try_run", "Runs a keyed background job only if the same key is not already in flight.")]
    public bool TryRun(string jobName, string key, string requestId, Table? payload = null)
        => _asyncLuaJobService.TryRun(jobName, key, requestId, payload);
}
