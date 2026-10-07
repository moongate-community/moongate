using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class SkillScriptServiceTests : IDisposable
{
    private readonly TemporaryDirectory _scripts = new();
    private readonly FakeScriptEngine _engine = new();
    private readonly MobileEntity _aria = new() { Id = new Serial(2), Name = "Aria" };

    [Fact]
    public async Task StartAsync_LoadsTheSkillScripts()
    {
        _scripts.CreateFile("skills/hiding.lua");
        _scripts.CreateFile("items/potion.lua");

        await Create().StartAsync();

        Assert.Equal(["skills/hiding.lua"], _engine.Loaded);
    }

    [Theory]
    [InlineData(SkillType.Hiding, "hiding")]
    [InlineData(SkillType.AnimalLore, "animal_lore")]
    public async Task Use_CallsOnUseOfTheTableNamedAfterTheSkill_WithTheUser(SkillType skill, string name)
    {
        var service = Create();
        await service.StartAsync();

        service.Use(skill, _aria);

        var call = Assert.Single(_engine.MemberCalls);
        Assert.Equal(($"skills/{name}.lua", name, "on_use"), (call.Owner, call.Table, call.Function));
        Assert.Equal([2L], call.Args);
    }

    [Fact]
    public async Task Use_BeforeTheStartOrAfterTheStop_IsMissing()
    {
        var service = Create();

        Assert.Equal(ScriptResultKind.Missing, service.Use(SkillType.Hiding, _aria).Kind);
        await service.StartAsync();
        await service.StopAsync();
        Assert.Equal(ScriptResultKind.Missing, service.Use(SkillType.Hiding, _aria).Kind);
        Assert.Empty(_engine.MemberCalls);
    }

    [Fact]
    public async Task Use_ANumberThatIsNoSkill_IsMissing()
    {
        var service = Create();
        await service.StartAsync();

        Assert.Equal(ScriptResultKind.Missing, service.Use((SkillType)250, _aria).Kind);
        Assert.Empty(_engine.MemberCalls);
    }

    private SkillScriptService Create()
    {
        return new(_engine, new StubGameLoop(), new ScriptEngineOptions { ScriptsDirectory = _scripts.Path });
    }

    public void Dispose()
    {
        _scripts.Dispose();
    }
}
