using Moongate.Scripting.Loaders;
using Moongate.Tests.TestSupport;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Scripting;

public class LuaScriptLoaderTests
{
    [Test]
    public void LoadFile_WhenModuleDoesNotExist_ShouldReturnNull()
    {
        using var temp = new TempDirectory();
        var loader = new LuaScriptLoader(temp.Path);
        var table = new Table(new());

        var content = loader.LoadFile("missing_module", table);

        Assert.That(content, Is.Null);
    }

    [Test]
    public void LoadFile_WhenModuleExists_ShouldReturnContent()
    {
        using var temp = new TempDirectory();
        var scriptsDir = temp.Path;
        var expected = "return 99";
        File.WriteAllText(Path.Combine(scriptsDir, "math_module.lua"), expected);
        var loader = new LuaScriptLoader(scriptsDir);
        var table = new Table(new());

        var content = loader.LoadFile("math_module", table);

        Assert.That(content, Is.EqualTo(expected));
    }

    [Test]
    public void LoadFile_WhenPluginNamespacedModuleExists_ShouldReturnPluginModuleContent()
    {
        using var temp = new TempDirectory();
        var scriptsDir = Path.Combine(temp.Path, "scripts");
        var pluginsDir = Path.Combine(temp.Path, "plugins");
        Directory.CreateDirectory(scriptsDir);
        Directory.CreateDirectory(Path.Combine(pluginsDir, "helpplus", "gumps"));
        var expected = "return { title = 'helpplus' }";
        File.WriteAllText(Path.Combine(pluginsDir, "helpplus", "gumps", "window.lua"), expected);
        var loader = new LuaScriptLoader(scriptsDir, pluginsDir);
        var table = new Table(new());

        var content = loader.LoadFile("plugin.helpplus.gumps.window", table);

        Assert.That(content, Is.EqualTo(expected));
    }

    [Test]
    public void ScriptFileExists_WhenModuleExists_ShouldReturnTrue()
    {
        using var temp = new TempDirectory();
        var scriptsDir = temp.Path;
        File.WriteAllText(Path.Combine(scriptsDir, "test_module.lua"), "return 42");
        var loader = new LuaScriptLoader(scriptsDir);

        var exists = loader.ScriptFileExists("test_module");

        Assert.That(exists, Is.True);
    }
}
