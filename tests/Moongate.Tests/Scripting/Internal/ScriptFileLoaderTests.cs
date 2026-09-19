using Lua;
using Lua.Standard;
using Moongate.Scripting.Internal;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Scripting.Internal;

public sealed class ScriptFileLoaderTests
{
    private static LuaState NewState()
    {
        var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenModuleLibrary();

        return state;
    }

    [Fact]
    public void Load_ExecutesTheFileAndTracksIt()
    {
        using var scripts = new TemporaryScriptsDirectory();
        scripts.Write("init.lua", "value = 41 + 1 return value");
        using var state = NewState();
        var loader = new ScriptFileLoader(state, scripts.Path);

        var result = loader.Load("init.lua", default);

        Assert.Equal(42, result[0].Read<double>());
        Assert.Equal(42, state.Environment["value"].Read<double>());
        Assert.Contains("init.lua", loader.LoadedFiles);
        Assert.Equal(1, loader.FilesLoaded);
    }

    [Fact]
    public void Load_ExposesTheCurrentFileWhileItRuns()
    {
        using var scripts = new TemporaryScriptsDirectory();
        scripts.Write("ai/guard.lua", "captured = probe()");
        using var state = NewState();
        var loader = new ScriptFileLoader(state, scripts.Path);
        string? seen = null;
        state.Environment["probe"] = new LuaFunction("probe", (context, _) => { seen = loader.CurrentFile; return new ValueTask<int>(context.Return()); });

        loader.Load("ai/guard.lua", default);

        Assert.Equal("ai/guard.lua", seen);
        Assert.Null(loader.CurrentFile);
    }

    [Fact]
    public void Load_ThenInvalidate_ReadsTheChangedFile()
    {
        using var scripts = new TemporaryScriptsDirectory();
        scripts.Write("a.lua", "return 1");
        using var state = NewState();
        var loader = new ScriptFileLoader(state, scripts.Path);
        loader.Load("a.lua", default);
        scripts.Write("a.lua", "return 2");

        var cached = loader.Load("a.lua", default);
        var invalidated = loader.Invalidate("a.lua");
        var reloaded = loader.Load("a.lua", default);

        Assert.Equal(1, cached[0].Read<double>());
        Assert.True(invalidated);
        Assert.Equal(2, reloaded[0].Read<double>());
        Assert.False(loader.Invalidate("never-loaded.lua"));
    }

    [Fact]
    public void Invalidate_EvictsTheModuleFromRequire()
    {
        using var scripts = new TemporaryScriptsDirectory();
        scripts.Write("common/util.lua", "return { v = 1 }");
        scripts.Write("init.lua", "return require('common.util').v");
        using var state = NewState();
        state.ModuleLoader = new ScriptDirectoryModuleLoader(scripts.Path);
        var loader = new ScriptFileLoader(state, scripts.Path);
        Assert.Equal(1, loader.Load("init.lua", default)[0].Read<double>());
        scripts.Write("common/util.lua", "return { v = 2 }");

        loader.Invalidate("common/util.lua");
        loader.Invalidate("init.lua");

        Assert.Equal(2, loader.Load("init.lua", default)[0].Read<double>());
    }

    [Fact]
    public void Load_MissingFile_ThrowsFileNotFound()
    {
        using var scripts = new TemporaryScriptsDirectory();
        using var state = NewState();

        Assert.Throws<FileNotFoundException>(() => new ScriptFileLoader(state, scripts.Path).Load("missing.lua", default));
    }

    [Fact]
    public void Load_SyntaxError_ThrowsLuaCompileExceptionNamingTheFile()
    {
        using var scripts = new TemporaryScriptsDirectory();
        scripts.Write("bad.lua", "this is not lua");
        using var state = NewState();

        var exception = Assert.Throws<LuaCompileException>(() => new ScriptFileLoader(state, scripts.Path).Load("bad.lua", default));

        Assert.Contains("bad.lua", exception.Message, StringComparison.Ordinal);
    }
}
