using Moongate.Core.Server.Interfaces.Services.Base;

namespace Moongate.Core.Server.Interfaces.Services;

public interface IScriptEngineService : IMoongateAutostartService
{
    void AddCallback(string name, Action<object[]> callback);
    void AddConstant(string name, object value);
    void AddInitScript(string script);
    void AddScriptModule(Type type);
    void ExecuteCallback(string name, params object[] args);
    void ExecuteScript(string script);
    void ExecuteScriptFile(string scriptFile);

    string ToScriptEngineFunctionName(string name);
}
