using Moongate.Server.Data.Scripting;
using Moongate.Server.Services.Scripting;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class LuaBrainRegistryTests
{
    [Test]
    public void Register_AndTryGet_ShouldResolveByBrainId()
    {
        var registry = new LuaBrainRegistry();
        var definition = new LuaBrainDefinition
        {
            BrainId = "orc_warrior",
            ScriptPath = "scripts/ai/orc_warrior.lua"
        };

        registry.Register(definition);
        var found = registry.TryGet("orc_warrior", out var resolved);

        Assert.Multiple(
            () =>
            {
                Assert.That(found, Is.True);
                Assert.That(resolved, Is.Not.Null);
                Assert.That(resolved!.BrainId, Is.EqualTo("orc_warrior"));
                Assert.That(resolved.ScriptPath, Is.EqualTo("scripts/ai/orc_warrior.lua"));
            }
        );
    }

    [Test]
    public void TryGet_WhenMissing_ShouldReturnFalse()
    {
        var registry = new LuaBrainRegistry();

        var found = registry.TryGet("missing_brain", out var resolved);

        Assert.Multiple(
            () =>
            {
                Assert.That(found, Is.False);
                Assert.That(resolved, Is.Null);
            }
        );
    }
}
