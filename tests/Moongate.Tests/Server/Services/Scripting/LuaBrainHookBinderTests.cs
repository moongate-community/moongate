using Moongate.Server.Services.Scripting.Internal;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class LuaBrainHookBinderTests
{
    [Test]
    public void TryBind_WhenOnThinkExists_ShouldResolveThinkAndCombatOutcomeHooks()
    {
        var script = new Script();
        script.DoString(
            """
            combat_brain = {
                on_think = function(npc_id) coroutine.yield(250) end,
                on_attack = function(target_id, context) end,
                on_missed_attack = function(target_id, context) end,
                on_attacked = function(attacker_id, context) end,
                on_missed_by_attack = function(attacker_id, context) end
            }
            """
        );

        var bound = LuaBrainHookBinder.TryBind(script, "combat_brain", out var hooks);

        Assert.Multiple(
            () =>
            {
                Assert.That(bound, Is.True);
                Assert.That(hooks.OnThinkFunction, Is.Not.Null);
                Assert.That(hooks.OnAttackFunction, Is.Not.Null);
                Assert.That(hooks.OnMissedAttackFunction, Is.Not.Null);
                Assert.That(hooks.OnAttackedFunction, Is.Not.Null);
                Assert.That(hooks.OnMissedByAttackFunction, Is.Not.Null);
            }
        );
    }

    [Test]
    public void TryBind_WhenOnlyBrainLoopExists_ShouldNotResolveOnThink()
    {
        var script = new Script();
        script.DoString(
            """
            legacy_brain = {
                brain_loop = function(npc_id) coroutine.yield(250) end
            }
            """
        );

        var bound = LuaBrainHookBinder.TryBind(script, "legacy_brain", out var hooks);

        Assert.Multiple(
            () =>
            {
                Assert.That(bound, Is.True);
                Assert.That(hooks.OnThinkFunction, Is.Null);
            }
        );
    }
}
