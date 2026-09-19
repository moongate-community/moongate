using Lua;
using Lua.Standard;
using Moongate.Scripting.Internal;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Scripting.Internal;

public sealed class ScriptDirectoryModuleLoaderTests
{
    [Fact]
    public void Require_ResolvesDotsToFoldersUnderTheScriptsDirectory()
    {
        using var scripts = new TemporaryScriptsDirectory();
        scripts.Write("common/dialogue.lua", "return { greeting = 'hi' }");
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenModuleLibrary();
        state.ModuleLoader = new ScriptDirectoryModuleLoader(scripts.Path);

        var result = SyncValueTask.Run(state.DoStringAsync("return require('common.dialogue').greeting", "t", default));

        Assert.Equal("hi", result[0].Read<string>());
    }

    [Fact]
    public void Require_MissingModule_RaisesALuaError()
    {
        using var scripts = new TemporaryScriptsDirectory();
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenModuleLibrary();
        state.ModuleLoader = new ScriptDirectoryModuleLoader(scripts.Path);

        Assert.Throws<LuaRuntimeException>(() => SyncValueTask.Run(state.DoStringAsync("return require('nope')", "t", default)));
    }

    [Theory, InlineData("../secret"), InlineData("..%2Fsecret"), InlineData("/etc/passwd"), InlineData("a/../../b")]
    public void ResolvePath_RejectsAnythingLeavingTheDirectory(string relativePath)
    {
        using var scripts = new TemporaryScriptsDirectory();

        Assert.Throws<InvalidOperationException>(() => ScriptDirectoryModuleLoader.ResolvePath(scripts.Path, relativePath));
    }

    [Fact]
    public void ResolvePath_KeepsAPathInsideTheDirectory()
    {
        using var scripts = new TemporaryScriptsDirectory();

        var resolved = ScriptDirectoryModuleLoader.ResolvePath(scripts.Path, "ai/guard.lua");

        Assert.StartsWith(Path.GetFullPath(scripts.Path), resolved, StringComparison.Ordinal);
        Assert.EndsWith("guard.lua", resolved, StringComparison.Ordinal);
    }
}
