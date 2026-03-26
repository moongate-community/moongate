namespace Moongate.Tests.Server.Services.Scripting;

public sealed class StandardAiBrainAssetTests
{
    [Test]
    public void RuntimeNamespaceAssets_ShouldExistAndBeRegisteredFromAiInit()
    {
        var repositoryRoot = GetRepositoryRoot();
        var initPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "init.lua");
        var requiredAssets = new[]
        {
            "moongate_data/scripts/ai/runtime/fsm.lua",
            "moongate_data/scripts/ai/runtime/targeting.lua",
            "moongate_data/scripts/ai/runtime/movement.lua",
            "moongate_data/scripts/ai/brains/ai_melee.lua",
            "moongate_data/scripts/ai/brains/ai_archer.lua",
            "moongate_data/scripts/ai/brains/ai_animal.lua",
            "moongate_data/scripts/ai/brains/ai_vendor.lua",
            "moongate_data/scripts/ai/brains/ai_berserk.lua",
            "moongate_data/scripts/ai/brains/ai_mage.lua",
            "moongate_data/scripts/ai/brains/ai_healer.lua",
            "moongate_data/scripts/ai/brains/ai_thief.lua",
        };
        var requiredInitRequires = new[]
        {
            "require(\"ai.runtime.fsm\")",
            "require(\"ai.runtime.targeting\")",
            "require(\"ai.runtime.movement\")",
            "require(\"ai.brains.ai_melee\")",
            "require(\"ai.brains.ai_archer\")",
            "require(\"ai.brains.ai_animal\")",
            "require(\"ai.brains.ai_vendor\")",
            "require(\"ai.brains.ai_berserk\")",
            "require(\"ai.brains.ai_mage\")",
            "require(\"ai.brains.ai_healer\")",
            "require(\"ai.brains.ai_thief\")",
        };

        Assert.That(File.Exists(initPath), Is.True);

        var init = File.ReadAllText(initPath);

        Assert.Multiple(
            () =>
            {
                foreach (var asset in requiredAssets)
                {
                    Assert.That(File.Exists(Path.Combine(repositoryRoot, asset)), Is.True, asset);
                }

                foreach (var requiredInitRequire in requiredInitRequires)
                {
                    Assert.That(init, Does.Contain(requiredInitRequire));
                }
            }
        );
    }

    [Test]
    public void FullStandardAiAssetSuite_ShouldNotReferenceModernUoRuntimeNamespace()
    {
        var repositoryRoot = GetRepositoryRoot();
        var brainPaths = new[]
        {
            "moongate_data/scripts/ai/brains/ai_animal.lua",
            "moongate_data/scripts/ai/brains/ai_archer.lua",
            "moongate_data/scripts/ai/brains/ai_berserk.lua",
            "moongate_data/scripts/ai/brains/ai_healer.lua",
            "moongate_data/scripts/ai/brains/ai_mage.lua",
            "moongate_data/scripts/ai/brains/ai_melee.lua",
            "moongate_data/scripts/ai/brains/ai_thief.lua",
            "moongate_data/scripts/ai/brains/ai_vendor.lua",
        };

        Assert.Multiple(
            () =>
            {
                foreach (var relativePath in brainPaths)
                {
                    var scriptPath = Path.Combine(repositoryRoot, relativePath);
                    var script = File.ReadAllText(scriptPath);

                    Assert.That(script, Does.Not.Contain("ai.modernuo."), relativePath);
                }
            }
        );
    }

    [Test]
    public void ModernUoFsm_ShouldDefineRequiredActionNames()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "modernuo", "fsm.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("wander"));
                Assert.That(script, Does.Contain("guard"));
                Assert.That(script, Does.Contain("combat"));
                Assert.That(script, Does.Contain("flee"));
                Assert.That(script, Does.Contain("backoff"));
                Assert.That(script, Does.Contain("interact"));
            }
        );
    }

    [Test]
    public void AiMelee_ShouldUseBestTargetAndFightRangeContracts()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "ai_melee.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("perception.find_best_target("));
                Assert.That(script, Does.Contain("mobile.get_ai_range_fight("));
                Assert.That(script, Does.Contain("combat.clear_target("));
            }
        );
    }

    [Test]
    public void AiArcher_ShouldUseFightAndAttackRangeContracts()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "ai_archer.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("mobile.get_ai_range_fight("));
                Assert.That(script, Does.Contain("combat.get_attack_range("));
                Assert.That(script, Does.Contain("backoff"));
            }
        );
    }

    [Test]
    public void AiAnimal_ShouldUseFleeThresholdAndHostileCombat()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "ai_animal.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("npc_state.get_hp_percent("));
                Assert.That(script, Does.Contain("perception.find_best_target("));
                Assert.That(script, Does.Contain("flee"));
            }
        );
    }

    [Test]
    public void AiVendor_ShouldUseInteractionStateAndFightModeNoneSemantics()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "ai_vendor.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("mobile.get_ai_fight_mode("));
                Assert.That(script, Does.Contain("\"none\""));
                Assert.That(script, Does.Contain("interact"));
                Assert.That(script, Does.Contain("Guards!"));
                Assert.That(script, Does.Contain("movement.flee("));
                Assert.That(script, Does.Contain("on_missed_by_attack"));
            }
        );
    }

    [Test]
    public void AiBerserk_ShouldUseAggressiveMeleeSpacing()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "ai_berserk.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("perception.find_best_target("));
                Assert.That(script, Does.Contain("combat"));
                Assert.That(script, Does.Contain("0"));
            }
        );
    }

    [Test]
    public void ModernUoAliasBrains_ShouldDelegateToLegacyBrains()
    {
        var repositoryRoot = GetRepositoryRoot();
        var magePath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "ai_mage.lua");
        var healerPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "ai_healer.lua");
        var thiefPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "ai", "brains", "ai_thief.lua");

        var mage = File.ReadAllText(magePath);
        var healer = File.ReadAllText(healerPath);
        var thief = File.ReadAllText(thiefPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(mage, Does.Contain("require(\"ai.brains.mage_combat\")"));
                Assert.That(mage, Does.Contain("ai_mage"));
                Assert.That(healer, Does.Contain("require(\"ai.brains.healer\")"));
                Assert.That(healer, Does.Contain("ai_healer"));
                Assert.That(thief, Does.Contain("require(\"ai.brains.thief\")"));
                Assert.That(thief, Does.Contain("ai_thief"));
            }
        );
    }

    [Test]
    public void ShippedLuaBrains_ShouldExposeOnThinkAndNotBrainLoop()
    {
        var repositoryRoot = GetRepositoryRoot();
        var relativePaths = new[]
        {
            "moongate_data/scripts/ai/brains/ai_animal.lua",
            "moongate_data/scripts/ai/brains/ai_archer.lua",
            "moongate_data/scripts/ai/brains/ai_berserk.lua",
            "moongate_data/scripts/ai/brains/ai_melee.lua",
            "moongate_data/scripts/ai/brains/ai_vendor.lua",
            "moongate_data/scripts/ai/brains/animal.lua",
            "moongate_data/scripts/ai/brains/berserk_combat.lua",
            "moongate_data/scripts/ai/brains/guard.lua",
            "moongate_data/scripts/ai/brains/healer.lua",
            "moongate_data/scripts/ai/brains/mage_combat.lua",
            "moongate_data/scripts/ai/brains/melee_combat.lua",
            "moongate_data/scripts/ai/brains/predator.lua",
            "moongate_data/scripts/ai/brains/ranged_combat.lua",
            "moongate_data/scripts/ai/brains/test_state_brain.lua",
            "moongate_data/scripts/ai/brains/thief.lua",
            "moongate_data/scripts/ai/brains/undead_melee.lua",
            "moongate_data/scripts/ai/brains/utility_npc.lua",
            "moongate_data/scripts/ai/brains/vendor.lua",
            "moongate_data/scripts/ai/npcs/lilly.lua",
            "moongate_data/scripts/ai/npcs/orion.lua",
            "moongate_data/scripts/ai/npcs/vega.lua",
        };

        Assert.Multiple(
            () =>
            {
                foreach (var relativePath in relativePaths)
                {
                    var scriptPath = Path.Combine(repositoryRoot, relativePath);
                    var script = File.ReadAllText(scriptPath);

                    Assert.That(script, Does.Contain(".on_think"), relativePath);
                    Assert.That(script, Does.Not.Contain(".brain_loop"), relativePath);
                }
            }
        );
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
}
