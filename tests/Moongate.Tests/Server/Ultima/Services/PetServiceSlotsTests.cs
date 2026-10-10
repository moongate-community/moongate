using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Taming;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class PetServiceSlotsTests : IAsyncLifetime
{
    private BroadcastFixture _fixture = null!;
    private PetService _service = null!;

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _service = new(
            _fixture.Mobiles,
            TestItems.Create(),
            new TamingService(
                new StubDataLoaderService().With(
                    new TamingCreature { Template = "horse", MinSkill = 30, Slots = 2, Food = ["grain"] }
                )
            ),
            new PetsConfig(),
            templates: new MobileTemplateService(
                new StubDataLoaderService().With(
                    new MobileTemplate { Id = "horse", ControlSlots = 4 },
                    new MobileTemplate { Id = "airele_summon", ControlSlots = 2 },
                    new MobileTemplate { Id = "cat" }
                )
            )
        );
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    public void SlotsOf_ATamableCreature_KeepsTheTamingEntry()
    {
        Assert.Equal(2, _service.SlotsOf("horse"));
    }

    [Fact]
    public void SlotsOf_ACreatureOnlyTheTemplateCounts_UsesTheControlSlotsOfTheTemplate()
    {
        Assert.Equal(2, _service.SlotsOf("airele_summon"));
    }

    [Fact]
    public void SlotsOf_ATemplateWithNoSlots_OrNoTemplate_IsOne()
    {
        Assert.Equal(1, _service.SlotsOf("cat"));
        Assert.Equal(1, _service.SlotsOf("nothing"));
        Assert.Equal(1, _service.SlotsOf(null));
    }
}
