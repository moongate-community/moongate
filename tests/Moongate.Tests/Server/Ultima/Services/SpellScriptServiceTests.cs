using Moongate.Scripting.Data.Config;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class SpellScriptServiceTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"spell-scripts-{Guid.NewGuid():N}");
    private readonly FakeScriptEngine _engine = new();
    private SpellScriptService _scripts = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(_root, SpellScriptService.SpellsDirectory));
        File.WriteAllText(Path.Combine(_root, SpellScriptService.SpellsDirectory, "heal.lua"), "heal = {}");
        _scripts = new(_engine, new StubGameLoop(), new ScriptEngineOptions { ScriptsDirectory = _root });
        await _scripts.StartAsync();
    }

    public Task DisposeAsync()
    {
        Directory.Delete(_root, true);

        return Task.CompletedTask;
    }

    [Fact]
    public void Has_ASpellWhoseFileLoadedAndHoldsCast_IsTrue()
    {
        _engine.Members = [("heal", "cast")];

        Assert.True(_scripts.Has(new SpellDefinition { Key = "heal" }));
    }

    [Fact]
    public void Has_AFileThatDidNotLoad_OrHasNoCast_IsFalse_SoNothingIsSpentForIt()
    {
        _engine.Members = [("heal", "check")];

        Assert.False(_scripts.Has(new SpellDefinition { Key = "heal" }));

        _engine.Members = [];

        Assert.False(_scripts.Has(new SpellDefinition { Key = "heal" }));
    }

    [Fact]
    public void Has_ASpellWithNoFile_IsFalse()
    {
        Assert.False(_scripts.Has(new SpellDefinition { Key = "recall" }));
    }
}
