using Lua;
using Lua.Standard;
using Moongate.Scripting.Internal;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Scripting.Internal;

public sealed class ScriptDirectoryModuleLoaderTests
{
    [Fact]
    public void Require_MissingModule_RaisesALuaError()
    {
        using var scripts = new TemporaryScriptsDirectory();
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenModuleLibrary();
        state.ModuleLoader = new ScriptDirectoryModuleLoader(scripts.Path);

        Assert.Throws<LuaRuntimeException>(
            () =>
                SyncValueTask.Run(state.DoStringAsync("return require('nope')", "t"))
        );
    }

    [Fact]
    public void Require_ResolvesDotsToFoldersUnderTheScriptsDirectory()
    {
        using var scripts = new TemporaryScriptsDirectory();
        scripts.Write("common/dialogue.lua", "return { greeting = 'hi' }");
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenModuleLibrary();
        state.ModuleLoader = new ScriptDirectoryModuleLoader(scripts.Path);

        var result = SyncValueTask.Run(state.DoStringAsync("return require('common.dialogue').greeting", "t"));

        Assert.Equal("hi", result[0].Read<string>());
    }

    [Fact]
    public void ResolvePath_AcceptsALinkWhoseTargetStaysInside()
    {
        using var scripts = new TemporaryScriptsDirectory();
        var real = scripts.Write("common/util.lua", "return 'fine'");

        if (!TryLink(Path.Combine(scripts.Path, "alias.lua"), real))
        {
            return;
        }

        var resolved = ScriptDirectoryModuleLoader.ResolvePath(scripts.Path, "alias.lua");

        Assert.EndsWith("alias.lua", resolved, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvePath_KeepsAPathInsideTheDirectory()
    {
        using var scripts = new TemporaryScriptsDirectory();

        var resolved = ScriptDirectoryModuleLoader.ResolvePath(scripts.Path, "ai/guard.lua");

        Assert.StartsWith(Path.GetFullPath(scripts.Path), resolved, StringComparison.Ordinal);
        Assert.EndsWith("guard.lua", resolved, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvePath_RejectsALinkWhoseTargetLeavesTheDirectory()
    {
        using var scripts = new TemporaryScriptsDirectory();
        using var outside = new TemporaryScriptsDirectory();
        var secret = outside.Write("secret.lua", "return 'leaked'");

        if (!TryLink(Path.Combine(scripts.Path, "leak.lua"), secret))
        {
            return;
        }

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => ScriptDirectoryModuleLoader.ResolvePath(scripts.Path, "leak.lua")
            );

        Assert.Contains("through a link", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvePath_RejectsAPathThroughALinkedDirectoryThatLeaves()
    {
        using var scripts = new TemporaryScriptsDirectory();
        using var outside = new TemporaryScriptsDirectory();
        outside.Write("lib/util.lua", "return 'leaked'");

        if (!TryLink(Path.Combine(scripts.Path, "shared"), Path.Combine(outside.Path, "lib"), true))
        {
            return;
        }

        Assert.Throws<InvalidOperationException>(
            () =>
                ScriptDirectoryModuleLoader.ResolvePath(scripts.Path, "shared/util.lua")
        );
    }

    [Theory, InlineData("../secret"), InlineData("/etc/passwd"), InlineData("a/../../b")]
    public void ResolvePath_RejectsAnythingLeavingTheDirectory(string relativePath)
    {
        using var scripts = new TemporaryScriptsDirectory();

        Assert.Throws<InvalidOperationException>(() => ScriptDirectoryModuleLoader.ResolvePath(scripts.Path, relativePath));
    }

    [Theory, InlineData("100%25.lua"), InlineData("..%2Fsecret.lua")]
    public void ResolvePath_TakesThePathAsWritten_WithoutPercentDecoding(string relativePath)
    {
        using var scripts = new TemporaryScriptsDirectory();

        var resolved = ScriptDirectoryModuleLoader.ResolvePath(scripts.Path, relativePath);

        // Decoding would rename "100%25.lua" to "100%.lua" and turn "..%2Fsecret.lua" into a traversal;
        // both are ordinary file names inside the directory.
        Assert.StartsWith(Path.GetFullPath(scripts.Path), resolved, StringComparison.Ordinal);
        Assert.EndsWith(relativePath, resolved, StringComparison.Ordinal);
    }

    [Theory, InlineData("common/dialogue.lua", "common.dialogue"), InlineData("init.lua", "init"),
     InlineData("ai/npc/guard.lua", "ai.npc.guard"), InlineData("data", "data")]
    public void ToModuleName_IsTheInverseOfTheNameToPathMapping(string normalizedRelativePath, string expected)
    {
        Assert.Equal(expected, ScriptDirectoryModuleLoader.ToModuleName(normalizedRelativePath));
    }

    private static bool TryLink(string path, string target, bool directory = false)
    {
        try
        {
            if (directory)
            {
                Directory.CreateSymbolicLink(path, target);
            }
            else
            {
                File.CreateSymbolicLink(path, target);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or
                                                       UnauthorizedAccessException or
                                                       PlatformNotSupportedException)
        {
            // Creating links needs a privilege on some platforms; the containment check is then untestable here.
            return false;
        }
    }
}
