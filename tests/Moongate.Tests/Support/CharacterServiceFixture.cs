using Moongate.Core.Interfaces;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Services.Accounts;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Mobiles;
using Moongate.Server.Services.World;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Types;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Tests.Support;

/// <summary>
/// Wires a real <see cref="CharacterService" /> over a fake persistence layer, with the minimum data a
/// character needs to be created: one starting city, one skill, the two container templates and a small
/// starting kit. Shared by the character-service tests and the login integration test so both exercise
/// the same graph.
/// </summary>
public static class CharacterServiceFixture
{
    public static StartingCityService Cities()
    {
        var service = new StartingCityService();
        service.Register(
            new()
            {
                City = "Britain", Building = "Inn", Description = 1, X = 1602, Y = 1591, Z = 20, Map = MapType.Trammel
            }
        );

        return service;
    }

    public static CharacterService Create(
        FakePersistenceService persistence,
        IEventBus eventBus,
        ISessionManager? sessions = null,
        IStartingCityService? cities = null,
        ILoopAffinity? loopAffinity = null
    )
    {
        var templates = Templates();
        var random = new Random(1);

        return new(
            persistence,
            new MobileFactoryService(cities ?? Cities(), new MobileTemplateService(), random),
            new ItemFactoryService(templates, random),
            new ItemService(persistence),
            templates,
            StartingItems(),
            Skills(),
            random,
            eventBus,
            sessions ?? new StubSessionManager(),
            loopAffinity
        );
    }

    public static SkillService Skills()
    {
        var service = new SkillService();
        service.Register(new() { Id = 1, Name = "Anatomy" });

        return service;
    }

    public static StartingItemsService StartingItems()
    {
        var service = new StartingItemsService();
        service.Load(
            new()
            {
                All = new() { Pack = [new() { Item = "dagger" }] },
                ByBody =
                {
                    ["Elf/Female"] = new() { Equip = [new() { Item = "shirt", Hue = "shirt" }] }
                }
            }
        );

        return service;
    }

    public static ItemTemplateService Templates()
    {
        var templates = new ItemTemplateService();
        templates.Register(new() { Id = "backpack", Name = "Backpack", Category = "Containers", ItemId = 3701 });
        templates.Register(new() { Id = "bank_box", Name = "Bank Box", Category = "Containers", ItemId = 2475 });
        templates.Register(new() { Id = "dagger", Name = "Dagger", Category = "Weapons", ItemId = 3921 });
        templates.Register(
            new()
            {
                Id = "shirt", Name = "Shirt", Category = "Clothing", ItemId = 5399,
                Equip = new() { Layer = LayerType.Shirt }
            }
        );

        return templates;
    }
}
