using System.Text.Json;
using Moongate.Server.Data.Entities;
using Moongate.Server.Services.Entities;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Professions;
using Moongate.UO.Data.Races.Base;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server;

public class StartupCompositionServiceTests
{
    [Test]
    public void Compose_ShouldApplyBaseRaceGenderAndProfessionRules()
    {
        var startupTemplateService = new StartupTemplateService();
        SeedDefaultTemplates(startupTemplateService);
        var service = new StartupCompositionService(startupTemplateService, new PlaceholderResolverService());
        var context = CreateContext("Mage", GenderType.Female);

        var loadout = service.Compose(context, "Tester");

        Assert.Multiple(
            () =>
            {
                Assert.That(loadout.Backpack.Any(static item => item.TemplateId == "RedBook"), Is.True);
                Assert.That(loadout.Backpack.Any(static item => item.TemplateId == "Gold" && item.Amount == 1000), Is.True);
                Assert.That(loadout.Equip.Any(static item => item.TemplateId == "Robe"), Is.True);
                Assert.That(loadout.Equip.Any(static item => item.TemplateId == "Shoes"), Is.True);
                Assert.That(loadout.Equip.Any(static item => item.TemplateId == "Skirt"), Is.True);
                Assert.That(loadout.Equip.Any(static item => item.TemplateId == "LongPants"), Is.False);
                Assert.That(loadout.Equip.Any(static item => item.TemplateId == "Dagger"), Is.False);
            }
        );
    }

    [Test]
    public void Compose_ShouldReplacePlayerNamePlaceholderAndKeepJsonTypes()
    {
        var startupTemplateService = new StartupTemplateService();
        SeedDefaultTemplates(startupTemplateService);
        var service = new StartupCompositionService(startupTemplateService, new PlaceholderResolverService());
        var context = CreateContext("Mage", GenderType.Male);

        var loadout = service.Compose(context, "Nakama");
        var book = loadout.Backpack.First(item => item.TemplateId == "RedBook");

        Assert.That(book.Args.HasValue, Is.True);
        var args = book.Args!.Value;

        Assert.Multiple(
            () =>
            {
                Assert.That(args.GetProperty("author").GetString(), Is.EqualTo("Nakama"));
                Assert.That(args.GetProperty("pages").GetInt32(), Is.EqualTo(20));
                Assert.That(args.GetProperty("writable").GetBoolean(), Is.True);
            }
        );
    }

    [Test]
    public void Compose_ShouldApplyRaceSpecificRemovalRules()
    {
        var startupTemplateService = new StartupTemplateService();
        SeedDefaultTemplates(startupTemplateService);
        var service = new StartupCompositionService(startupTemplateService, new PlaceholderResolverService());
        var context = CreateContext("Warrior", GenderType.Male, new TestRace("gargoyle", 2));

        var loadout = service.Compose(context, "Garg");

        Assert.Multiple(
            () =>
            {
                Assert.That(loadout.Equip.Any(static item => item.TemplateId == "Shoes"), Is.False);
                Assert.That(loadout.Equip.Any(static item => item.TemplateId == "GargishDagger"), Is.True);
            }
        );
    }

    [Test]
    public void Compose_WhenTemplatesMissing_ShouldReturnEmptyLoadout()
    {
        var service = new StartupCompositionService(new StartupTemplateService(), new PlaceholderResolverService());
        var context = CreateContext("Warrior", GenderType.Male);

        var loadout = service.Compose(context, "Tester");

        Assert.Multiple(
            () =>
            {
                Assert.That(loadout.Backpack, Is.Empty);
                Assert.That(loadout.Equip, Is.Empty);
            }
        );
    }

    private static StarterProfileContext CreateContext(string professionName, GenderType gender, Race? race = null)
    {
        var profession = new ProfessionInfo
        {
            ID = 1,
            Name = professionName
        };

        return new(profession, race, gender);
    }

    [Test]
    public void Compose_ShouldResolveExtendedPlaceholders()
    {
        var startupTemplateService = new StartupTemplateService();
        SeedDefaultTemplates(startupTemplateService);
        startupTemplateService.Upsert(
            "startup_professions",
            JsonSerializer.SerializeToElement(
                new
                {
                    rules = new object[]
                    {
                        new
                        {
                            match = new
                            {
                                profession = "Warrior"
                            },
                            add = new
                            {
                                backpack = new object[]
                                {
                                    new
                                    {
                                        templateId = "RedBook",
                                        args = new
                                        {
                                            title = "<professionName> Manual",
                                            author = "<playerName>",
                                            audience = "<raceName>-<gender>"
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            )
        );

        var service = new StartupCompositionService(startupTemplateService, new PlaceholderResolverService());
        var context = CreateContext("Warrior", GenderType.Female, new TestRace("elf", 1));

        var loadout = service.Compose(context, "Lyra");
        var book = loadout.Backpack.Last(item => item.TemplateId == "RedBook");
        var args = book.Args!.Value;

        Assert.Multiple(
            () =>
            {
                Assert.That(args.GetProperty("title").GetString(), Is.EqualTo("Warrior Manual"));
                Assert.That(args.GetProperty("author").GetString(), Is.EqualTo("Lyra"));
                Assert.That(args.GetProperty("audience").GetString(), Is.EqualTo("elf-female"));
            }
        );
    }

    private static void SeedDefaultTemplates(StartupTemplateService startupTemplateService)
    {
        startupTemplateService.Upsert(
            "startup_base",
            JsonSerializer.SerializeToElement(
                new
                {
                    schema = "moongate.startup.base.v1",
                    backpack = new object[]
                    {
                        new
                        {
                            templateId = "RedBook",
                            args = new
                            {
                                title = "a book",
                                author = "<playerName>",
                                pages = 20,
                                writable = true
                            }
                        },
                        new
                        {
                            templateId = "Gold",
                            amount = 1000
                        }
                    },
                    equip = new object[]
                    {
                        new
                        {
                            templateId = "Dagger"
                        }
                    }
                }
            )
        );

        startupTemplateService.Upsert(
            "startup_races",
            JsonSerializer.SerializeToElement(
                new
                {
                    schema = "moongate.startup.races.v1",
                    rules = new object[]
                    {
                        new
                        {
                            match = new
                            {
                                race = "human"
                            },
                            add = new
                            {
                                equip = new object[]
                                {
                                    new
                                    {
                                        templateId = "Shoes"
                                    }
                                }
                            }
                        },
                        new
                        {
                            match = new
                            {
                                race = "gargoyle"
                            },
                            add = new
                            {
                                equip = new object[]
                                {
                                    new
                                    {
                                        templateId = "GargishDagger"
                                    }
                                }
                            },
                            remove = new
                            {
                                equip = new[] { "Shoes" }
                            }
                        }
                    }
                }
            )
        );

        startupTemplateService.Upsert(
            "startup_genders",
            JsonSerializer.SerializeToElement(
                new
                {
                    schema = "moongate.startup.genders.v1",
                    rules = new object[]
                    {
                        new
                        {
                            match = new
                            {
                                gender = "male"
                            },
                            add = new
                            {
                                equip = new object[]
                                {
                                    new
                                    {
                                        templateId = "LongPants"
                                    }
                                }
                            }
                        },
                        new
                        {
                            match = new
                            {
                                gender = "female"
                            },
                            add = new
                            {
                                equip = new object[]
                                {
                                    new
                                    {
                                        templateId = "Skirt"
                                    }
                                }
                            },
                            remove = new
                            {
                                equip = new[] { "LongPants" }
                            }
                        }
                    }
                }
            )
        );

        startupTemplateService.Upsert(
            "startup_professions",
            JsonSerializer.SerializeToElement(
                new
                {
                    schema = "moongate.startup.professions.v1",
                    rules = new object[]
                    {
                        new
                        {
                            match = new
                            {
                                profession = "Mage"
                            },
                            add = new
                            {
                                equip = new object[]
                                {
                                    new
                                    {
                                        templateId = "Robe"
                                    }
                                }
                            },
                            remove = new
                            {
                                equip = new[] { "Dagger" }
                            }
                        }
                    }
                }
            )
        );
    }
}
