using Moongate.Server.Ultima.Data.Pets;
using Moongate.Server.Ultima.Data.Taming;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class PetFoodServiceTests
{
    private readonly PetFoodService _service;

    public PetFoodServiceTests()
    {
        var data = new StubDataLoaderService().With(
            new TamingCreature { Template = "horse", MinSkill = 29.1, Food = ["fruit", "grain"] },
            new TamingCreature { Template = "dog", MinSkill = 10 },
            new TamingCreature { Template = "lizard", MinSkill = 80, Food = [] }
        );
        data.With(
            new PetFood { Kind = "fruit", Items = ["apple"] },
            new PetFood { Kind = "grain", Items = ["bread"] },
            new PetFood { Kind = "meat", Items = ["ham"] }
        );
        _service = new(data, new TamingService(data));
    }

    [Theory,
     InlineData("horse", "apple", true),
     InlineData("horse", "bread", true),
     InlineData("horse", "ham", false),
     InlineData("dog", "ham", true),
     InlineData("dog", "apple", false),
     InlineData("lizard", "ham", false),
     InlineData("orc", "ham", false),
     InlineData("dog", "sword", false),
     InlineData(null, "ham", false),
     InlineData("dog", null, false)]
    public void Accepts_OnlyTheKindsTheCreatureEats(string? creature, string? item, bool expected)
    {
        Assert.Equal(expected, _service.Accepts(creature, item));
    }
}
