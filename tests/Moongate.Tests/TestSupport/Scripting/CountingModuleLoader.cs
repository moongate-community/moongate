using Lua;

namespace Moongate.Tests.TestSupport.Scripting;

/// <summary>Serves one module named "m" from a source factory and lets tests count how often it was loaded.</summary>
public sealed class CountingModuleLoader : ILuaModuleLoader
{
    private readonly Func<string> _source;

    public CountingModuleLoader(Func<string> source)
    {
        _source = source;
    }

    public bool Exists(string moduleName)
    {
        return moduleName == "m";
    }

    public ValueTask<LuaModule> LoadAsync(string moduleName, CancellationToken cancellationToken)
    {
        return new ValueTask<LuaModule>(new LuaModule(moduleName, _source()));
    }
}
