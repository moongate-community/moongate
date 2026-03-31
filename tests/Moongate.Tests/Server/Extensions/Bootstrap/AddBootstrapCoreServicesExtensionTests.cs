using DryIoc;
using Moongate.Server.Extensions.Bootstrap;
using Moongate.Server.Services.Magic;
using Moongate.Server.Services.Magic.Spells.Magery.First;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Services.Scripting;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Interfaces.Templates;
using Moongate.UO.Data.Services.Templates;

namespace Moongate.Tests.Server.Extensions.Bootstrap;

[TestFixture]
public sealed class AddBootstrapCoreServicesExtensionTests
{
    [Test]
    public void AddBootstrapCoreServices_SeedsMagicRegistryWithImplementedFirstCircleSpells()
    {
        using var container = new Container();

        container.AddBootstrapCoreServices();

        var registry = container.Resolve<SpellRegistry>();

        Assert.Multiple(() =>
        {
            Assert.That(registry.Get(SpellIds.Magery.First.Clumsy), Is.TypeOf<ClumsySpell>());
            Assert.That(registry.Get(SpellIds.Magery.First.Feeblemind), Is.TypeOf<FeeblemindSpell>());
            Assert.That(registry.Get(SpellIds.Magery.First.Heal), Is.TypeOf<HealSpell>());
            Assert.That(registry.Get(SpellIds.Magery.First.MagicArrow), Is.TypeOf<MagicArrowSpell>());
            Assert.That(registry.Get(SpellIds.Magery.First.NightSight), Is.TypeOf<NightSightSpell>());
            Assert.That(registry.Get(SpellIds.Magery.First.ReactiveArmor), Is.TypeOf<ReactiveArmorSpell>());
            Assert.That(registry.Get(SpellIds.Magery.First.Weaken), Is.TypeOf<WeakenSpell>());
            Assert.That(registry.Get(SpellIds.Magery.First.CreateFood), Is.Null);
        });
    }

    [Test]
    public void AddBootstrapCoreServices_RegistersQuestTemplateService()
    {
        using var container = new Container();

        container.AddBootstrapCoreServices();

        var service = container.Resolve<IQuestTemplateService>();

        Assert.That(service, Is.TypeOf<QuestTemplateService>());
    }

    [Test]
    public void AddBootstrapCoreServices_RegistersQuestDefinitionService()
    {
        using var container = new Container();

        container.AddBootstrapCoreServices();

        var service = container.Resolve<IQuestDefinitionService>();

        Assert.That(service, Is.TypeOf<QuestDefinitionService>());
    }
}
