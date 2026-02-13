using Jint;
using Jint.Native;
using Moongate.Core.Server.Interfaces.Services;

namespace Moongate.Server.Wraps;

public class BaseWrap
{
    protected JsValue Value { get; set; }

    private readonly IScriptEngineService _scriptEngineService;

    public BaseWrap(IScriptEngineService scriptEngineService, JsValue jsValue)
    {
        _scriptEngineService = scriptEngineService;
        Value = jsValue;
    }

    public TOut Call<TOut>(string methodName, params object[] args)
        => Call(methodName, args).ToObject() is TOut result ? result : default!;

    protected JsValue Call(string methodName, params object[] args)
    {
        var method = Value.Get(_scriptEngineService.ToScriptEngineFunctionName(methodName));

        return Value.AsObject()
                    .Engine
                    .Call(
                        method,
                        Value.AsObject(),
                        args.Select(arg => JsValue.FromObject(Value.AsObject().Engine, arg)).ToArray()
                    );
    }
}
