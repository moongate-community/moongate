using Moongate.Scripting.Interfaces;
using Moongate.Scripting.Services;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Server.Interfaces.Services.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Server.Services.Scripting;

internal sealed class AsyncLuaJobService : IAsyncLuaJobService
{
    private const string QueueName = "lua-async-job";
    private const string ResultCallbackName = "on_async_job_result";
    private const string ErrorCallbackName = "on_async_job_error";

    private readonly IAsyncWorkSchedulerService _asyncWorkSchedulerService;
    private readonly AsyncLuaValueConverter _converter;
    private readonly IAsyncLuaJobRegistry _jobRegistry;
    private readonly IScriptEngineService _scriptEngineService;
    private readonly Script _luaScript;

    public AsyncLuaJobService(
        IAsyncWorkSchedulerService asyncWorkSchedulerService,
        IAsyncLuaJobRegistry jobRegistry,
        IScriptEngineService scriptEngineService,
        AsyncLuaValueConverter converter
    )
    {
        _asyncWorkSchedulerService =
            asyncWorkSchedulerService ?? throw new ArgumentNullException(nameof(asyncWorkSchedulerService));
        _jobRegistry = jobRegistry ?? throw new ArgumentNullException(nameof(jobRegistry));
        _scriptEngineService = scriptEngineService ?? throw new ArgumentNullException(nameof(scriptEngineService));
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _luaScript = (scriptEngineService as LuaScriptEngineService)?.LuaScript ??
                     throw new ArgumentException(
                         "AsyncLuaJobService requires LuaScriptEngineService.",
                         nameof(scriptEngineService)
                     );
    }

    public bool Run(string jobName, string requestId, Table? payload = null)
        => Schedule(jobName, requestId, Guid.NewGuid().ToString("N"), payload);

    public bool TryRun(string jobName, string key, string requestId, Table? payload = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return Schedule(jobName, requestId, key.Trim(), payload);
    }

    private void DispatchError(AsyncLuaJobRequest request, Exception ex)
        => _scriptEngineService.CallFunction(ErrorCallbackName, request.JobName, request.RequestId, ex.Message);

    private void DispatchResult(AsyncLuaJobResult result)
    {
        var luaResult = _converter.ToLuaTable(_luaScript, result.Payload);
        _scriptEngineService.CallFunction(ResultCallbackName, result.JobName, result.RequestId, luaResult);
    }

    private async Task<AsyncLuaJobResult> ExecuteAsync(
        IAsyncLuaJobHandler handler,
        AsyncLuaJobRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await handler.ExecuteAsync(request.Payload, cancellationToken).ConfigureAwait(false);

        return new(request.JobName, request.RequestId, result);
    }

    private bool Schedule(string jobName, string requestId, string scheduleKey, Table? payload)
    {
        if (string.IsNullOrWhiteSpace(jobName) ||
            string.IsNullOrWhiteSpace(requestId) ||
            !_jobRegistry.TryResolve(jobName, out var handler) ||
            handler is null ||
            !_converter.TryConvertPayload(payload, out var convertedPayload))
        {
            return false;
        }

        var request = new AsyncLuaJobRequest(jobName.Trim(), requestId.Trim(), scheduleKey, convertedPayload);

        return _asyncWorkSchedulerService.TrySchedule(
            QueueName,
            scheduleKey,
            token => ExecuteAsync(handler, request, token),
            result => DispatchResult(result),
            ex => DispatchError(request, ex)
        );
    }
}
