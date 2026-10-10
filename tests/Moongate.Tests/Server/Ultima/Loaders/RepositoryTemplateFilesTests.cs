using Moongate.Server.Ultima.Packets.Books;
using Moongate.Core.Directories;
using Moongate.Core.Serialization.Toml;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Commands;
using Moongate.Server.Ultima.Data.Books;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Loaders;
using Moongate.Server.Ultima.Types.Guilds;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Services.Internal.Books;
using Moongate.Server.Ultima.Services.Motd;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Loaders;

/// <summary>
///     Loads the templates shipped in <c>moongate_root/templates</c> with the real loaders, so a broken template fails
///     here instead of at the next server start.
/// </summary>
public sealed class RepositoryTemplateFilesTests
{
    [Theory]
    [InlineData("eng")]
    [InlineData("ita")]
    [InlineData("fre")]
    [InlineData("ger")]
    [InlineData("spa")]
    [InlineData("por")]
    [InlineData("pol")]
    [InlineData("cze")]
    public async Task AttachmentMessages_ExistInEveryLanguageWithoutFallback(string language)
    {
        var path = Path.Combine(FindRepositoryRoot(), "moongate_root", "data", "messages", language, "moongate.toml");
        var source = await TomlUtils.DeserializeFromFileAsync<Moongate.Server.Ultima.Data.Messages.MessageContentFile>(path);
        foreach (var id in Enumerable.Range(BookAttachmentService.ClaimLabelMessage, 6))
        {
            Assert.True(source!.Messages.TryGetValue(id.ToString(), out var text));
            Assert.False(string.IsNullOrWhiteSpace(text));
        }

        if (language == "ita")
            Assert.Equal("Ritira allegati", source!.Messages[BookAttachmentService.ClaimLabelMessage.ToString()]);
        Assert.NotEmpty(
            (await new MessagesLoader(Directories(), new LocalizationConfig { Language = language }).LoadDataAsync())
            .Entities
        );
    }

    [Fact]
    public async Task ShippedBookTemplates_LoadAgainstRealReadableItems()
    {
        var items = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToArray();
        var books = (await new BooksLoader(Directories(), new StubDataLoaderService().With(items)).LoadDataAsync()).Entities;
        Assert.Equal(66, books.Count);
        var blank = Assert.Single(books, book => book.Id == "blank_book");
        Assert.Equal(
            ("a book", "$player_name", "readable_book", true, 20, ""),
            (blank.Title, blank.Author, blank.ItemTemplate, blank.Writable, blank.Pages, blank.Content)
        );
        Assert.Equal(
            ["jail_release_note", "welcome_letter"],
            books.Where(book => book.Id is "jail_release_note" or "welcome_letter")
                .Select(book => book.Id)
                .Order(StringComparer.Ordinal)
        );
        var welcome = Assert.Single(books, book => book.Id == "welcome_letter");
        Assert.Equal(["contact_name"], welcome.Variables);
        Assert.Equal("readable_scroll", welcome.ItemTemplate);
        Assert.Equal(false, items.Single(item => item.Id == "readable_scroll").Stackable);
        Assert.Equal(0x14EDu, items.Single(item => item.Id == "readable_scroll").ItemId.Value);
        Assert.Equal(7, books.Single(book => book.Id == "jail_release_note").Translations.Count);
    }

    [Fact]
    public async Task ShippedModernUoBookTemplates_RenderAllBooksAndKeepJournalPartsDistinct()
    {
        var items = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToArray();
        var data = new StubDataLoaderService().With(items);
        var catalog = ImportedBookIds();
        var books = (await new BooksLoader(Directories(), data).LoadDataAsync()).Entities
            .Where(book => catalog.Contains(book.Id))
            .ToArray();
        Assert.Equal(62, books.Length);
        Assert.Equal(9, books.Count(book => book.Id.StartsWith("grimmoch_journal", StringComparison.Ordinal)));
        Assert.Equal(6, books.Count(book => book.Id.StartsWith("lysander_notebook", StringComparison.Ordinal)));
        Assert.Equal(13, books.Count(book => book.Id.StartsWith("tavaras_journal", StringComparison.Ordinal)));
        Assert.Equal("Yorick of Yew", books.Single(book => book.Id == "grammar_of_orcish").Author);
        Assert.Equal(7457, books.Single(book => book.Id == "my_story").Content.Length);
        data.With(books);
        var service = new BookTemplateService(data);
        foreach (var book in books)
        {
            Assert.Empty(book.Variables);
            Assert.Empty(book.Attachments);
            Assert.Equal(
                ["cze", "fre", "ger", "ita", "pol", "por", "spa"],
                book.Translations.Keys.Order(StringComparer.Ordinal)
            );
            Assert.True(
                service.TryRender(
                    book.Id,
                    new TextTemplateContext { PlayerName = "Changed reader" },
                    "eng",
                    null,
                    out var rendered
                ),
                book.Id
            );
            Assert.Equal((book.Title, book.Author, book.Content), (rendered!.Title, rendered.Author, rendered.Content));
            Assert.True(BookGumpRenderer.TryBuild(rendered.Title, rendered.Author, rendered.Content, out _), book.Id);
        }
    }

    // The imported texts are books of the client: each has a cover, and its pages fit the two book packets in
    // every language, a translated page of more than eight lines going on in the next.
    [Theory]
    [InlineData("eng")]
    [InlineData("ita")]
    [InlineData("fre")]
    [InlineData("ger")]
    [InlineData("spa")]
    [InlineData("por")]
    [InlineData("pol")]
    [InlineData("cze")]
    public async Task ShippedModernUoBooks_AreBooksWithACover_AndTheirPagesFitTheClient(string language)
    {
        var items = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToArray();
        var data = new StubDataLoaderService().With(items);
        var catalog = ImportedBookIds();
        var books = (await new BooksLoader(Directories(), data).LoadDataAsync()).Entities
            .Where(book => catalog.Contains(book.Id))
            .ToArray();
        data.With(books);
        var service = new BookTemplateService(data);
        Assert.Equal("readable_book", Assert.Single(items, item => item.Id == "readable_book").ScriptId);
        // ModernUO's own count of pages.
        Assert.Equal(738, books.Sum(book => book.Content.Split("\n\n").Length));

        foreach (var book in books)
        {
            Assert.Equal("readable_book", book.ItemTemplate);
            Assert.Contains(book.ItemId, new int?[] { 0x0FEF, 0x0FF0, 0x0FF1, 0x0FF2 });
            Assert.True(service.TryRender(book.Id, new TextTemplateContext(), language, null, out var rendered), book.Id);
            // The pages of ModernUO, in every language: a blank line inside a page is a line of one space, so only
            // a page break is an empty line.
            Assert.Equal(book.Content.Split("\n\n").Length, rendered!.Content.Split("\n\n").Length);
            Assert.True(BookPagination.TryPaginate(rendered.Content, out var pages), book.Id);
            Assert.All(pages, page => Assert.InRange(page.Count, 0, BookPagination.LinesPerPage));
            Assert.Equal(
                rendered.Content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries),
                pages.SelectMany(page => page).SelectMany(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            );
            Assert.Equal(pages.Count, new BookPagesPacket(new(0x40000001), pages).PageCount);
            Assert.Equal(
                pages.Count,
                new BookHeaderPacket(new(0x40000001), pages.Count, rendered.Title, rendered.Author).PageCount
            );
        }
    }

    [Theory]
    [InlineData("ita")]
    [InlineData("fre")]
    [InlineData("ger")]
    [InlineData("spa")]
    [InlineData("por")]
    [InlineData("pol")]
    [InlineData("cze")]
    public async Task ShippedModernUoTranslations_RenderCompleteCatalogInEachLanguage(string language)
    {
        var items = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToArray();
        var data = new StubDataLoaderService().With(items);
        var catalog = ImportedBookIds();
        var books = (await new BooksLoader(Directories(), data).LoadDataAsync()).Entities
            .Where(book => catalog.Contains(book.Id))
            .ToArray();
        data.With(books);
        var service = new BookTemplateService(data);
        Assert.Equal(62, books.Length);
        foreach (var book in books)
        {
            Assert.True(book.Translations.TryGetValue(language, out var translation), $"{book.Id}: {language} missing");
            Assert.False(string.IsNullOrWhiteSpace(translation!.Title), book.Id);
            Assert.False(string.IsNullOrWhiteSpace(translation.Content), book.Id);
            Assert.Null(translation.Author);
            Assert.True(
                service.TryRender(
                    book.Id,
                    new TextTemplateContext { PlayerName = "Another reader" },
                    language,
                    null,
                    out var rendered
                ),
                book.Id
            );
            Assert.Equal(translation.Title, rendered!.Title);
            Assert.Equal(translation.Content, rendered.Content);
            Assert.Equal(book.Author, rendered.Author);
            Assert.NotEqual(book.Content, rendered.Content);
            Assert.True(
                BookGumpRenderer.TryBuild(rendered.Title, rendered.Author, rendered.Content, out _),
                $"{book.Id}: {language}"
            );
        }
    }

    [Fact]
    public async Task ShippedMotd_Loads()
    {
        var registry = new MotdVariableRegistry();
        MotdRenderer.RegisterBuiltins(registry);
        var loader = new MotdLoader(Directories(), registry);

        Assert.NotEmpty((await loader.LoadDataAsync()).Entities);
    }

    [Fact]
    public async Task ShippedTitles_LoadCompleteClassicGrid()
    {
        var rows = (await new TitlesLoader(Directories()).LoadDataAsync()).Entities;

        Assert.Equal(55, rows.Count);
        Assert.Contains(rows, row => row.Fame == 0 && row.Karma == -15000 && row.Title == "The Outcast");
        Assert.Contains(
            rows,
            row => row.Fame == 10000 && row.Karma == 10000 &&
                   row.Title == "The Glorious Lord" && row.FemaleTitle == "The Glorious Lady"
        );
    }

    public RepositoryTemplateFilesTests()
    {
        TomlUtils.AddTomlConverter(new SerialTomlConverter());
        TomlUtils.AddTomlConverter(new HueSpecTomlConverter());
        TomlUtils.AddTomlConverter(new EnumValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new RangeValueSpecTomlConverterFactory());
        TomlUtils.AddTomlConverter(new DiceSpecTomlConverter());
    }

    [Fact]
    public async Task ShippedItemTemplates_LoadAndResolve()
    {
        var templates = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        Assert.True(templates.Count > 7000, $"only {templates.Count} templates");
        Assert.Equal(0x0E75u, templates["0x0e75_backpack"].ItemId.Value);
        Assert.True(templates["0x0eed_gold_coin"].Stackable);

        // Only abstract base templates may resolve to no graphic; anything else would spawn invisible.
        Assert.Empty(templates.Values.Where(t => t.ItemId.Value == 0 && !t.Id.Contains("base")).Select(t => t.Id));
    }

    [Fact]
    public async Task ShippedDecorationTemplates_AreFixedAndDoNotDecay_AndTheDoorHasTheDoorScript()
    {
        var templates = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        var decoration = templates["decoration"];
        var door = templates["decoration_door"];
        Assert.Equal(((bool?)false, (bool?)false, (string?)null), (decoration.Movable, decoration.Decays, decoration.Name));
        Assert.True(string.IsNullOrEmpty(decoration.ScriptId));
        Assert.Equal(((bool?)false, (bool?)false, "door"), (door.Movable, door.Decays, door.ScriptId));
        var light = templates["decoration_light"];
        Assert.Equal(((bool?)false, (bool?)false, "light"), (light.Movable, light.Decays, light.ScriptId));
        Assert.Equal("light", templates["0x0a28_candle"].ScriptId);
        Assert.True(templates.ContainsKey(Moongate.Server.Ultima.Commands.KeyCommand.KeyTemplate));
    }

    [Fact]
    public async Task ShippedItemTemplates_HaveTheCorpse_ThatCannotBePickedUpAndDecays()
    {
        var templates = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        var corpse = templates["corpse"];
        Assert.Equal(
            (0x2006u, (bool?)false, (bool?)true, (int?)7),
            (corpse.ItemId.Value, corpse.Movable, corpse.Decays, corpse.DecayMinutes)
        );
    }

    [Fact]
    public async Task ShippedItemTemplates_HaveTheDyesTheDyeTubAndDyeableClothing()
    {
        var templates = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        Assert.Equal(("dyes", "dye_tub"), (templates["0x0fa9_dyes"].ScriptId, templates["0x0fab_dying_tub"].ScriptId));
        // Clothing has it from its base; a weapon does not.
        Assert.True(templates["base_clothing"].Dyeable);
        Assert.True(templates["0x1541_body_sash"].Dyeable);
        Assert.Null(templates["0x13b2"].Dyeable);
        Assert.Contains(templates.Values, template => template.Dyeable == false);
    }

    [Fact]
    public async Task ShippedItemTemplates_MarkTwoHandedWeaponsButNotShieldsOrTorches()
    {
        var templates = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        // Bows, polearms, staves: both hands.
        Assert.True(templates["0x13b2"].TwoHandedWeapon);
        Assert.True(templates["base_halberd"].TwoHandedWeapon);
        Assert.True(templates["base_quarter_staff"].TwoHandedWeapon);

        // Shields and a torch share the layer but leave the other hand free.
        Assert.Null(templates["base_heater_shield"].TwoHandedWeapon);
        Assert.Null(templates["0x0f64_torch"].TwoHandedWeapon);
        Assert.All(
            templates.Values.Where(t =>
                t.Name?.EndsWith("shield", StringComparison.Ordinal) == true && t.Layer == LayerType.TwoHanded
            ),
            shield => Assert.Null(shield.TwoHandedWeapon)
        );
    }

    [Fact]
    public async Task ShippedStartingItems_ResolveAgainstTheShippedTemplates()
    {
        var directories = Directories();
        var templates = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var data = new StubDataLoaderService().With(templates);
        data.With((await new BooksLoader(directories, data).LoadDataAsync()).Entities.ToArray());
        var loader = new StartingItemsLoader(directories, data, new BookTemplateService(data), new LocalizationConfig());
        await loader.InitializeAsync();

        var sets = (await loader.LoadDataAsync()).Entities;

        Assert.Equal(56, sets.Count);
        var common = Assert.Single(sets, set => set.Common);
        var gold = Assert.Single(common.Items, entry => entry.Items.SequenceEqual([new ItemsConfig().GoldTemplate]));
        Assert.Equal(1000, gold.Amount!.Value.Roll());

        // Every new character can eat and drink: three loaves of bread and a pitcher of water, both with a script.
        var bread = Assert.Single(common.Items, entry => entry.Items.SequenceEqual(["0x103b_bread_loaf"]));
        Assert.Equal(3, bread.Amount!.Value.Roll());
        Assert.Single(common.Items, entry => entry.Items.SequenceEqual(["0x1f9e_pitcher_of_water"]));
        Assert.Equal("food", templates.Single(template => template.Id == "0x103b_bread_loaf").ScriptId);
        Assert.Equal("drink", templates.Single(template => template.Id == "0x1f9e_pitcher_of_water").ScriptId);
        // And can write: the blank book of ModernUO's new characters.
        Assert.Equal(["readable_book"], Assert.Single(common.Items, entry => entry.BookTemplate == "blank_book").Items);
        var letter = Assert.Single(common.Items, entry => entry.BookTemplate == "welcome_letter");
        Assert.Equal(["readable_scroll"], letter.Items);
        Assert.Equal("Vega", letter.BookValues["contact_name"]);
        Assert.False(letter.Equip);
        Assert.False(gold.Equip);
        Assert.Contains(templates, t => t.Id == new ItemsConfig().BackpackTemplate);
        Assert.Contains(templates, t => t.Id == new ItemsConfig().GoldTemplate);
    }

    [Fact]
    public async Task ShippedMobileTemplates_LoadAgainstTheShippedNamesAndItems()
    {
        var directories = Directories();
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync())
            .Entities.ToArray();
        var loader = new MobileTemplatesLoader(directories, new StubDataLoaderService().With(names).With(items).With(loots));

        var mobiles = (await loader.LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        Assert.Equal(699, mobiles.Count);
        Assert.Equal("{gender}", mobiles["guard"].NameList);
        // Moongate's own cats inherit the UOX3 cat and add their name and script.
        Assert.Equal(
            (201, "Orione", "orione"),
            (mobiles["orione"].Body, mobiles["orione"].Name, mobiles["orione"].ScriptId)
        );
        Assert.Equal((201, "Vega", "vega"), (mobiles["vega"].Body, mobiles["vega"].Name, mobiles["vega"].ScriptId));
        var lilly = mobiles["lilly"];
        Assert.Equal(
            ("Lilly", "the Noble", 32000, 32000),
            (lilly.Name, lilly.Title, lilly.Fame!.Value.Roll(), lilly.Karma!.Value.Roll())
        );
        Assert.Equal(
            ["0x230e_gilded_dress", "0x1711_thigh_boots", "base_royal_circlet"],
            lilly.Equipment!.SelectMany(entry => entry.Items)
        );
    }

    [Fact]
    public async Task ShippedCreatures_UseTheScriptOfTheirKind()
    {
        var directories = Directories();
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync())
            .Entities.ToArray();
        var mobiles = (await new MobileTemplatesLoader(
                directories,
                new StubDataLoaderService().With(names).With(items).With(loots)
            ).LoadDataAsync())
            .Entities.ToDictionary(t => t.Id);

        // The creatures that go for the players, those that keep to themselves and those that run.
        Assert.All(
            new[] { "orc", "skeleton", "ogre", "lizardman", "dragon" },
            id => Assert.Equal("monster", mobiles[id].ScriptId)
        );
        Assert.Equal("scared_animal", mobiles["rabbit"].ScriptId);
        Assert.Contains(mobiles.Values, template => template.ScriptId == "animal");
        Assert.True(mobiles.Values.Count(template => template.ScriptId == "monster") > 200);
    }

    [Fact]
    public async Task ShippedArchers_HoldABow_AndTheUndeadAndTheElementalsNeverFlee()
    {
        var directories = Directories();
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync())
            .Entities.ToArray();
        var mobiles = (await new MobileTemplatesLoader(
                directories,
                new StubDataLoaderService().With(names).With(items).With(loots)
            ).LoadDataAsync())
            .Entities.ToDictionary(t => t.Id);

        // A ratman archer goes for the players, and what it holds is a bow: its item template shoots.
        var archer = mobiles["ratmanarcher"];
        Assert.Equal("monster", archer.ScriptId);
        var held = archer.Equipment!.SelectMany(entry => entry.Items)
            .Select(id =>
                items.FirstOrDefault(item => item.Id == id || item.Id.StartsWith(id + "_", StringComparison.Ordinal))
            );
        Assert.Contains(held, item => item?.WeaponType is WeaponType.Bow or WeaponType.Crossbow);

        // The guard of Ilshenar and Malas: the guard script, invulnerable, with a bow in its hands and the skill to use it.
        var guard = mobiles["archerguard"];
        Assert.Equal(("guard", NotorietyType.Invulnerable), (guard.ScriptId, guard.Notoriety));
        Assert.Contains(
            guard.Equipment!.SelectMany(entry => entry.Items)
                .Select(id =>
                    items.FirstOrDefault(item => item.Id == id || item.Id.StartsWith(id + "_", StringComparison.Ordinal))
                ),
            item => item?.WeaponType is WeaponType.Bow
        );
        Assert.True(guard.Skills!.ContainsKey("archery"));
        Assert.DoesNotContain(guard.Equipment!.SelectMany(entry => entry.Items), id => id == "guardhalberd");

        // UOX3's FLEEAT=-1, the creatures that never run.
        Assert.Equal(-1, mobiles["zombie"].FleeAt);
        Assert.Equal(-1, mobiles["skeleton"].FleeAt);
        Assert.True(mobiles.Values.Count(template => template.FleeAt == -1) > 40);
    }

    [Fact]
    public async Task ShippedBlood_IsSevenDecayingGroundItems_AndTheUndeadAndTheGolemsDoNotBleed()
    {
        var directories = Directories();
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync())
            .Entities.ToArray();
        var mobiles = (await new MobileTemplatesLoader(
                directories,
                new StubDataLoaderService().With(names).With(items).With(loots)
            ).LoadDataAsync())
            .Entities.ToDictionary(t => t.Id);

        // Each piece the blood service puts down is a template that is not movable and decays, and has a graphic.
        Assert.All(
            BloodService.Templates,
            id =>
            {
                var piece = Assert.Single(items, item => item.Id == id);
                Assert.Equal((false, true, true), (piece.Movable ?? true, piece.Decays ?? false, piece.ItemId.Value != 0));
            }
        );

        Assert.Equal(-1, mobiles["skeleton"].BloodHue);
        Assert.Equal(-1, mobiles["zombie"].BloodHue);
        Assert.Equal(-1, mobiles["golem"].BloodHue);
        Assert.Null(mobiles["orc"].BloodHue);
    }

    [Fact]
    public async Task ShippedDummiesAndButtes_UseTheirScripts_AndTheButtesAreShotFromAfar()
    {
        var items = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToDictionary(item => item.Id);

        Assert.All(
            new[] { "0x1070_training_dummy", "0x1071_training_dummy", "0x1074_training_dummy", "0x1075_training_dummy" },
            id => Assert.Equal("training_dummy", items[id].ScriptId)
        );
        Assert.All(
            new[] { "0x100a_archery_butte", "0x100b_archery_butte" },
            id => Assert.Equal(("archery_butte", (int?)6), (items[id].ScriptId, items[id].UseRange))
        );

        foreach (var script in new[] { "training_dummy.lua", "archery_butte.lua" })
        {
            Assert.True(File.Exists(Path.Combine(FindRepositoryRoot(), "moongate_root", "scripts", "items", script)));
        }
    }

    [Fact]
    public async Task ShippedLockpicksAndTreasureChests_UseTheirScripts_AndTheScriptsAreThere()
    {
        var items = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToDictionary(item => item.Id);

        Assert.All(
            new[] { "0x14fb_lockpick", "0x14fc_lockpick", "0x14fd_lockpicks", "0x14fe_lockpicks" },
            id => Assert.Equal("lockpick", items[id].ScriptId)
        );
        Assert.All(
            new[] { "treasure_chest_level_1", "treasure_chest_level_2", "treasure_chest_level_3", "treasure_chest_level_4" },
            id => Assert.Equal("treasure_chest", items[id].ScriptId)
        );

        foreach (var script in new[] { "lockpick.lua", "treasure_chest.lua" })
        {
            Assert.True(File.Exists(Path.Combine(FindRepositoryRoot(), "moongate_root", "scripts", "items", script)));
        }
    }

    [Fact]
    public async Task ShippedBandage_UsesTheBandageScript_AndTheScriptIsThere()
    {
        var items = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToArray();

        var bandage = Assert.Single(items, item => item.Id == "0x0e21_clean_bandage");

        Assert.Equal("bandage", bandage.ScriptId);
        Assert.True(File.Exists(Path.Combine(FindRepositoryRoot(), "moongate_root", "scripts", "items", "bandage.lua")));
    }

    [Fact]
    public async Task ShippedFishingPoles_UseTheFishingPoleScript_AndWhatItPullsOutIsThere()
    {
        var items = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToDictionary(item => item.Id);

        Assert.Equal("fishing_pole", items["0x0dbf_fishing_pole"].ScriptId);
        Assert.Equal("fishing_pole", items["0x0dc0_fishing_pole"].ScriptId);

        var script = await File.ReadAllTextAsync(
            Path.Combine(FindRepositoryRoot(), "moongate_root", "scripts", "items", "fishing_pole.lua")
        );
        var caught = System.Text.RegularExpressions.Regex.Matches(script, "template = \"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToArray();

        // Four fish and four pieces of footwear, each an item template of the distribution.
        Assert.Equal(8, caught.Length);
        Assert.All(caught, template => Assert.Contains(template, items.Keys));
    }

    [Fact]
    public async Task ShippedAxes_UseTheAxeScript_AndItsLogsAndWoodAreThere()
    {
        var directories = Directories();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToDictionary(item => item.Id);

        // The hatchet and the axe of every era take the script from their base; a war axe chops nothing.
        Assert.All(
            new[] { "0x0f43", "0x0f43_lbr", "0x0f43_t2a", "0x0f49", "0x0f49_aos", "0x13fb", "0x0f4b" },
            id => Assert.Equal("axe", items[id].ScriptId)
        );
        Assert.NotEqual("axe", items["0x13b0"].ScriptId);

        var script = await File.ReadAllTextAsync(
            Path.Combine(FindRepositoryRoot(), "moongate_root", "scripts", "common", "woods.lua")
        );
        var logs = System.Text.RegularExpressions.Regex.Match(script, "id = \"plain\", name = \"plain\", logs = \"([^\"]+)\"").Groups[1].Value;

        Assert.True(items[logs].Stackable);

        var wood = Assert.Single((await new HarvestLoader(directories).LoadDataAsync()).Entities, r => r.Id == "wood");
        Assert.Equal((4, 2, 4, 20, 30), (wood.Area, wood.AmountMin, wood.AmountMax, wood.RespawnMinMinutes, wood.RespawnMaxMinutes));
    }

    [Fact]
    public async Task ShippedBlades_UseTheBladeScript_AndBoardsAndKindlingAreThere()
    {
        var items = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToDictionary(item => item.Id);

        // A dagger, a skinning knife, a butcher knife and a long sword cut; an axe keeps its own script, and the
        // flower garland UOX3 binds to its blade script by mistake cuts nothing.
        Assert.All(new[] { "0x0f51", "0x0f52", "0x0ec4", "0x13f6", "0x0f61" }, id => Assert.Equal("blade", items[id].ScriptId));
        Assert.Equal("axe", items["0x0f49"].ScriptId);
        Assert.NotEqual("blade", items["0x2306"].ScriptId);
        // Every template with the script is a weapon.
        Assert.All(items.Values.Where(item => item.ScriptId == "blade"), item => Assert.NotNull(item.WeaponType));

        Assert.True(items["0x1bd7_board"].Stackable);
        Assert.True(items["0x0de1_kindling"].Stackable);
        Assert.True(items["0x1bdd_log"].Stackable);
    }

    [Fact]
    public async Task ShippedWoods_AreThere_WithTheHueOfTheirKind_AndTheVeinsTheAxeKnows()
    {
        var directories = Directories();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToDictionary(item => item.Id);
        var scripts = Path.Combine(FindRepositoryRoot(), "moongate_root", "scripts");
        var axe = await File.ReadAllTextAsync(Path.Combine(scripts, "items", "axe.lua")) +
                  await File.ReadAllTextAsync(Path.Combine(scripts, "common", "woods.lua"));
        var hues = new Dictionary<string, int>
        {
            ["oak"] = 0x7DA, ["ash"] = 0x4A7, ["yew"] = 0x4A8, ["heartwood"] = 0x4A9, ["bloodwood"] = 0x4AA,
            ["frostwood"] = 0x47F
        };

        foreach (var (kind, hue) in hues)
        {
            var (logs, boards) = (items[kind + "_log"], items[kind + "_board"]);

            Assert.Equal((0x1BE0u, 0x1BD7u), (logs.ItemId.Value, boards.ItemId.Value));
            Assert.Equal(((int?)hue, (int?)hue), (logs.Hue?.Min, boards.Hue?.Min));
            Assert.Equal(((bool?)true, (bool?)true), (logs.Stackable, boards.Stackable));
        }

        // Every template the scripts give is shipped: the logs and boards of the kinds, and the finds.
        var given = System.Text.RegularExpressions.Regex.Matches(axe, "(?:logs|boards|other_logs|other_boards|template) = \"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToArray();
        Assert.Equal(21, given.Length);
        Assert.All(given, id => Assert.True(items.ContainsKey(id), id));
        Assert.All(new[] { "bark_fragment", "brilliant_amber" }, id => Assert.True(items[id].Stackable));

        // And every vein of the wood but the plain one is a kind the script knows.
        var wood = (await new HarvestLoader(directories).LoadDataAsync()).Entities.Single(resource => resource.Id == "wood");
        Assert.Equal(["plain", "oak", "ash", "yew", "heartwood", "bloodwood", "frostwood"], wood.Vein.Select(vein => vein.Id));
        Assert.Equal(1000, wood.Vein.Sum(vein => vein.Weight));
        Assert.All(wood.Vein.Skip(1), vein => Assert.Contains($"{{ id = \"{vein.Id}\", name = ", axe));
    }

    [Fact]
    public async Task ShippedCrafts_Load_WithTheFortyTwoRecipesOfCarpentry_AndItsToolsCarryTheScript()
    {
        var directories = Directories();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var data = new StubDataLoaderService().With(items);
        var lists = (await new CraftResourcesLoader(directories, data).LoadDataAsync()).Entities.ToArray();
        data.With(lists);
        var crafts = (await new CraftsLoader(directories, data).LoadDataAsync()).Entities.ToDictionary(craft => craft.Id);
        var craft = crafts["carpentry"];

        Assert.Equal(("carpentry", "carpentry", 0x023D), (craft.Id, craft.Skill, craft.Sound));
        Assert.Equal(
            ["Chairs", "Tables", "Containers", "Other Items", "Staves & Poles", "Musical items"],
            craft.Group.Select(group => group.Name)
        );
        Assert.Equal(42, craft.Group.Sum(group => group.Recipe.Count));
        Assert.Equal(["0x1bd7_board", "0x1bda_board"], lists.Single(list => list.Id == "wood").Templates);

        var byId = items.ToDictionary(item => item.Id);
        Assert.All(new[] { "0x1034_saw", "0x1028_dovetail_saw", "0x10e5_froe" }, id => Assert.Equal("carpentry_tool", byId[id].ScriptId));
        Assert.NotEqual("carpentry_tool", byId["0x102e_nails"].ScriptId);

        // Blacksmithing: every era's groups, 66 recipes of iron; the hammers, sledges and tongs carry its tool script.
        var smithing = crafts["blacksmithing"];
        Assert.Equal(("blacksmithy", 0x002A), (smithing.Skill, smithing.Sound));
        Assert.Equal(
            ["Ringmail", "Chainmail", "Platemail", "Helmets", "Shields", "Bladed", "AOS Weapons", "Axes", "Polearms", "Bashing", "SE Weapons"],
            smithing.Group.Select(group => group.Name)
        );
        Assert.Equal(66, smithing.Group.Sum(group => group.Recipe.Count));
        Assert.All(
            new[] { "0x13e3", "0x13e4", "0x0fb4", "0x0fb5", "0x0fbb_tongs", "0x0fbc_tongs" },
            id => Assert.Equal("smithing_tool", byId[id].ScriptId)
        );
        Assert.NotEqual("smithing_tool", byId["0x0faf_anvil"].ScriptId);

        // Tailoring: eight groups, 50 recipes of cloth and leather; the sewing kits sew, the runic ones too, the scissors do not.
        var tailoring = crafts["tailoring"];
        Assert.Equal(
            ["Hats", "Shirts", "Pants", "Miscellaneous", "Footwear", "Leather Armor", "Studded Armor", "Female Armor"],
            tailoring.Group.Select(group => group.Name)
        );
        Assert.Equal(50, tailoring.Group.Sum(group => group.Recipe.Count));
        Assert.Equal("tailoring_tool", byId["0x0f9d_sewing_kit"].ScriptId);
        Assert.Equal("tailoring_tool", byId["spined_runic_sewing_kit"].ScriptId);
        Assert.NotEqual("tailoring_tool", byId["0x0f9e_scissors"].ScriptId);

        // Tinkering: seven groups (the traps left out), 59 recipes; the tinker's tools and tool kits work.
        var tinkering = crafts["tinkering"];
        Assert.Equal(
            ["Tools", "Parts", "Utensils", "Jewelry", "Miscellaneous", "More Tools", "Candles"],
            tinkering.Group.Select(group => group.Name)
        );
        Assert.Equal(59, tinkering.Group.Sum(group => group.Recipe.Count));
        Assert.All(new[] { "0x1ebc_tinker's_tools", "0x1eb8_tool_kit" }, id => Assert.Equal("tinkering_tool", byId[id].ScriptId));
        Assert.NotEqual("tinkering_tool", byId["taxidermykit"].ScriptId);

        var recipes = tinkering.Group.SelectMany(group => group.Recipe).ToList();
        Assert.Equal("0x1ebc_tinker's_tools", recipes.Single(recipe => recipe.Name == "Tinker's tools").Item);
        Assert.Equal(recipes.Count, recipes.Select(recipe => recipe.Name).Distinct().Count());

        // Bowcraft and fletching: the bows of its root, then shafts, arrows and bolts; the fletcher's tools work.
        var fletching = crafts["fletching"];
        Assert.Equal("bowcraft_fletching", fletching.Skill);
        Assert.Equal(["Weapons", "Shafts", "Arrows", "Crossbow Bolts"], fletching.Group.Select(group => group.Name));
        Assert.Equal(9, fletching.Group.Sum(group => group.Recipe.Count));
        Assert.All(new[] { "0x1022_fletcher's_tools", "0x1023_fletcher's_tools" }, id => Assert.Equal("fletching_tool", byId[id].ScriptId));
    }

    [Fact]
    public async Task ShippedMetals_AreThere_WithTheHueOfTheirMetal_AndTheVeinsTheScriptsKnow()
    {
        var directories = Directories();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToDictionary(item => item.Id);
        var metals = await File.ReadAllTextAsync(
            Path.Combine(FindRepositoryRoot(), "moongate_root", "scripts", "common", "metals.lua")
        );
        var hues = new Dictionary<string, int>
        {
            ["dull_copper"] = 0x973, ["shadow_iron"] = 0x966, ["copper"] = 0x96D, ["bronze"] = 0x972, ["gold"] = 0x8A5,
            ["agapite"] = 0x979, ["verite"] = 0x89F, ["valorite"] = 0x8AB
        };

        foreach (var (metal, hue) in hues)
        {
            var (ore, ingot) = (items["ore_" + metal], items["ingot_" + metal]);

            Assert.Equal((0x19B9u, 0x1BF2u), (ore.ItemId.Value, ingot.ItemId.Value));
            Assert.Equal(((int?)hue, (int?)hue), (ore.Hue?.Min, ingot.Hue?.Min));
            Assert.Equal("ore", ore.ScriptId);
            Assert.Equal(((bool?)true, (bool?)true), (ore.Stackable, ingot.Stackable));
        }

        // Every template the metals module names is shipped, and every vein of the ore but iron is a metal it knows.
        var named = System.Text.RegularExpressions.Regex.Matches(metals, "\"((?:ore|ingot)_[a-z_]+|0x[0-9a-f]{4}_iron_(?:ore|ingot))\"")
            .Select(match => match.Groups[1].Value)
            .ToArray();
        Assert.Equal(22, named.Length);
        Assert.All(named, id => Assert.True(items.ContainsKey(id), id));
        var veins = (await new HarvestLoader(directories).LoadDataAsync()).Entities.Single(resource => resource.Id == "ore").Vein;
        Assert.Equal(1000, veins.Sum(vein => vein.Weight));
        Assert.Equal("iron", veins[0].Id);
        Assert.All(veins.Skip(1), vein => Assert.Contains($"{{ id = \"{vein.Id}\",", metals));
    }

    [Fact]
    public async Task ShippedMiningToolsAndOre_UseTheirScripts_AndWhatTheyGiveIsThere()
    {
        var directories = Directories();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToDictionary(item => item.Id);

        Assert.All(
            new[] { "0x0e85_pickaxe", "0x0e86", "0x0f39_a_shovel", "0x0f3a" },
            id => Assert.Equal("pickaxe", items[id].ScriptId)
        );

        var scripts = Path.Combine(FindRepositoryRoot(), "moongate_root", "scripts", "items");
        var piles = System.Text.RegularExpressions.Regex
            .Matches(await File.ReadAllTextAsync(Path.Combine(scripts, "pickaxe.lua")), "template = \"([^\"]+)\"")
            .Select(match => match.Groups[1].Value)
            .ToArray();

        // The four piles a dig gives are ore, stack, and are smelted by the ore script.
        Assert.Equal(4, piles.Length);
        Assert.All(piles, pile => Assert.Equal(("ore", true), (items[pile].ScriptId, items[pile].Stackable)));

        // The ingots of iron are the first the metals module names.
        var ingot = System.Text.RegularExpressions.Regex
            .Match(await File.ReadAllTextAsync(Path.Combine(scripts, "..", "common", "metals.lua")), "ingot = \"([^\"]+)\"")
            .Groups[1]
            .Value;
        Assert.True(items[ingot].Stackable);

        var ore = Assert.Single((await new HarvestLoader(directories).LoadDataAsync()).Entities, r => r.Id == "ore");
        Assert.Equal((8, 10, 34, 10, 20), (ore.Area, ore.AmountMin, ore.AmountMax, ore.RespawnMinMinutes, ore.RespawnMaxMinutes));
    }

    [Fact]
    public async Task ShippedHarvest_HasTheFishOfTheFishingPoles()
    {
        var resources = (await new HarvestLoader(Directories()).LoadDataAsync()).Entities.ToArray();

        var fish = Assert.Single(resources, resource => resource.Id == "fish");
        Assert.Equal((8, 5, 15, 10, 20), (fish.Area, fish.AmountMin, fish.AmountMax, fish.RespawnMinMinutes, fish.RespawnMaxMinutes));
    }

    [Fact]
    public async Task ShippedHealers_UseTheHealerScript()
    {
        var directories = Directories();
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync())
            .Entities.ToArray();
        var mobiles = (await new MobileTemplatesLoader(
                directories,
                new StubDataLoaderService().With(names).With(items).With(loots)
            ).LoadDataAsync())
            .Entities.ToDictionary(t => t.Id);

        Assert.All(
            new[] { "healer", "m_healer", "f_healer", "whealer", "m_whealer", "f_whealer" },
            id => Assert.Equal("healer", mobiles[id].ScriptId)
        );
        // The evil ones inherit the script and the shop, and are known by their id.
        Assert.All(new[] { "evilhealer", "evilwhealer" }, id => Assert.Equal("healer", mobiles[id].ScriptId));
        Assert.Equal("the Evil Healer", mobiles["evilhealer"].Title);
    }

    [Fact]
    public async Task ShippedVendorsBankersAndGuards_AreInvulnerable_AsInModernUO()
    {
        var directories = Directories();
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync())
            .Entities.ToArray();
        var mobiles = (await new MobileTemplatesLoader(
                directories,
                new StubDataLoaderService().With(names).With(items).With(loots)
            ).LoadDataAsync())
            .Entities.ToDictionary(t => t.Id);

        // Every template that inherits the base vendor, and the bankers and the guards, which do not.
        var vendors = mobiles.Values.Where(t => t.Id == "basevendor" || IsVendor(t, mobiles)).ToList();
        Assert.True(vendors.Count > 100);
        Assert.All(vendors, t => Assert.True(t.Notoriety == NotorietyType.Invulnerable, $"{t.Id} is not invulnerable"));
        Assert.All(
            new[]
            {
                "banker", "m_banker", "f_banker", "gypsybanker", "m_gypsybanker", "f_gypsybanker", "guard", "m_guard",
                "f_guard"
            },
            id => Assert.Equal(NotorietyType.Invulnerable, mobiles[id].Notoriety)
        );
        // The townfolk and the monsters are not.
        Assert.NotEqual(NotorietyType.Invulnerable, mobiles["skeleton"].Notoriety);
    }

    [Fact]
    public async Task ShippedShops_LoadAgainstTheShippedItemsAndMobiles_AndTheirVendorsHaveTheShopkeeperScript()
    {
        var directories = Directories();
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync())
            .Entities.ToArray();
        var mobiles = (await new MobileTemplatesLoader(
                directories,
                new StubDataLoaderService().With(names).With(items).With(loots)
            )
            .LoadDataAsync()).Entities.ToArray();

        var shops = (await new ShopsLoader(directories, new StubDataLoaderService().With(items).With(mobiles))
            .LoadDataAsync()).Entities;

        Assert.Contains(shops, shop => shop.Id == "baker" && shop.Buy.Count > 0);
        Assert.Contains(shops, shop => shop.Id == "baker" && shop.Sell.Any(line => line.Item == "0x103b_bread_loaf"));
        var scripts = mobiles.ToDictionary(template => template.Id, template => template.ScriptId);
        // A vendor that is a banker or a healer keeps its own script; the others use the shopkeeper's.
        Assert.All(
            shops.SelectMany(shop => shop.Vendors),
            vendor => Assert.Contains(scripts[vendor], new[] { "shopkeeper", "healer", "banker" })
        );
        Assert.Equal("shopkeeper", scripts["m_baker"]);

        // Buying from one vendor and selling to another is never a profit.
        var lowestBuy = shops.SelectMany(shop => shop.Buy)
            .GroupBy(line => line.Item)
            .ToDictionary(group => group.Key, group => group.Min(line => line.Price));
        Assert.All(
            shops.SelectMany(shop => shop.Sell).Where(line => lowestBuy.ContainsKey(line.Item)),
            line => Assert.True(
                line.Price <= lowestBuy[line.Item],
                $"{line.Item} is bought at {line.Price} but sold from {lowestBuy[line.Item]}"
            )
        );
    }

    [Fact]
    public async Task ShippedGuildmasters_AreVendorsOfTheirTrade_WithAManAWomanAndAList()
    {
        var directories = Directories();
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync())
            .Entities.ToArray();
        var mobiles = (await new MobileTemplatesLoader(
                directories,
                new StubDataLoaderService().With(names).With(items).With(loots)
            )
            .LoadDataAsync()).Entities;

        var masters = mobiles.Where(template => template.Id.EndsWith("_guildmaster", StringComparison.Ordinal)).ToList();

        // Twelve trades, a man and a woman each.
        Assert.Equal(24, masters.Count);
        Assert.All(masters, master => Assert.Equal("basevendor", master.BaseId));
        // The miner's guildmaster names no guild in ModernUO: it teaches only.
        Assert.Equal(
            ["m_miner_guildmaster", "f_miner_guildmaster"],
            masters.Where(master => master.NpcGuild is null).Select(master => master.Id)
        );
        var smith = masters.Single(master => master.Id == "m_blacksmith_guildmaster");
        Assert.Equal(NpcGuildType.Blacksmiths, smith.NpcGuild);
        Assert.Equal("the blacksmith guildmaster", smith.Title);
        Assert.True(smith.Skills!.ContainsKey("blacksmithy"));
        Assert.Equal(
            "shopkeeper",
            mobiles.Single(template => template.Id == "m_blacksmith_guildmaster").ScriptId ?? "shopkeeper"
        );
    }

    [Fact]
    public async Task ShippedBankers_AllHaveTheBankerScript()
    {
        var directories = Directories();
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync())
            .Entities.ToArray();
        var loader = new MobileTemplatesLoader(directories, new StubDataLoaderService().With(names).With(items).With(loots));

        var bankers = (await loader.LoadDataAsync()).Entities
            .Where(template => template.Id.EndsWith("banker", StringComparison.Ordinal))
            .ToDictionary(template => template.Id, template => template.ScriptId);

        Assert.Equal(
            ["banker", "f_banker", "f_gypsybanker", "gypsybanker", "m_banker", "m_gypsybanker"],
            bankers.Keys.Order(StringComparer.Ordinal)
        );
        Assert.All(bankers.Values, script => Assert.Equal("banker", script));
    }

    [Fact]
    public async Task ShippedNpcListsAndSpawns_LoadAgainstTheShippedMobiles()
    {
        var directories = Directories();
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync())
            .Entities.ToArray();
        var mobiles = (await new MobileTemplatesLoader(
                directories,
                new StubDataLoaderService().With(names).With(items).With(loots)
            )
            .LoadDataAsync()).Entities.ToArray();
        var lists = (await new NpcListsLoader(directories, new StubDataLoaderService().With(mobiles)).LoadDataAsync())
            .Entities.ToArray();

        var spawns = (await new SpawnsLoader(directories, new StubDataLoaderService().With(mobiles).With(lists).With(items))
                .LoadDataAsync())
            .Entities.ToDictionary(spawn => spawn.Id);

        Assert.Equal(457, lists.Length);
        Assert.Equal(4677, spawns.Count);
        // The treasure chests of ModernUO's spawners: regions of items.
        var chests = spawns.Values.Where(spawn =>
                spawn.ItemIds.Count > 0 && !spawn.Id.StartsWith("felucca_jail_chest_", StringComparison.Ordinal)
            )
            .ToList();
        Assert.Equal(399, chests.Count);
        Assert.Equal(633, chests.Sum(chest => chest.Max));
        Assert.Equal(
            [(MapType.Felucca, 198), (MapType.Ilshenar, 3), (MapType.Trammel, 198)],
            chests.GroupBy(chest => chest.Map).Select(group => (group.Key, group.Count()))
        );
        Assert.All(chests, chest => Assert.All(chest.ItemIds, item => Assert.StartsWith("treasure_chest_level_", item)));
        var shop = spawns["felucca_0"];
        Assert.Equal(("The Hammer And Anvil", MapType.Felucca, 480), (shop.Name, shop.Map, shop.MinMinutes));
        Assert.Equal(["weaponsmith"], shop.MobileIds);
    }

    [Fact]
    public async Task ShippedGumps_LoadAndTheDecorationConfirmationOffersConfirmAndCancel()
    {
        var gumps = (await new GumpsLoader(Directories()).LoadDataAsync()).Entities.ToDictionary(gump => gump.Id);

        var rendered = GumpXmlRenderer.Render(gumps[DecorateCommand.ConfirmGump], new Dictionary<string, string>(), null);

        Assert.Equal(["cancel", DecorateCommand.ConfirmClick], rendered.Clicks.Values.Order());
    }

    [Fact]
    public async Task ShippedGuardAndOrc_CreateWithTheShippedRacesAndNames()
    {
        var directories = Directories();
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var races = (await new RacesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync())
            .Entities.ToArray();
        var mobiles = (await new MobileTemplatesLoader(
                directories,
                new StubDataLoaderService().With(names).With(items).With(loots)
            )
            .LoadDataAsync()).Entities.ToArray();
        var loaders = new StubDataLoaderService().With(names).With(races).With(mobiles);
        var factory = new MobileFactoryService(
            new MobileTemplateService(loaders),
            new NameService(loaders),
            loaders,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!
        );

        var guard = factory.Create("guard");
        var orc = factory.Create("orc");

        Assert.Contains(guard.Body, new[] { 400, 401 });
        Assert.False(string.IsNullOrWhiteSpace(guard.Name));
        Assert.True(orc.Body > 0 && orc.HitsMax > 0 && !string.IsNullOrWhiteSpace(orc.Name), $"{orc.Body} {orc.Name}");

        var lilly = factory.Create("lilly");
        Assert.Equal((401, "Lilly", 32000, 32000), (lilly.Body, lilly.Name, lilly.Fame, lilly.Karma));
    }

    [Fact]
    public async Task ShippedTreasureChests_AreFixedDecayAndHoldGoldAndLoot()
    {
        var templates = (await new ItemTemplatesLoader(Directories()).LoadDataAsync()).Entities.ToDictionary(t => t.Id);

        var chests = Enumerable.Range(1, 4).Select(level => templates[$"treasure_chest_level_{level}"]).ToList();

        Assert.All(
            chests,
            chest => Assert.Equal(((bool?)false, (bool?)true, (int?)45), (chest.Movable, chest.Decays, chest.DecayMinutes))
        );
        Assert.Equal([30, 70, 180, 200], chests.Select(chest => chest.Gold!.Value.Min));
        Assert.Equal([129, 169, 419, 599], chests.Select(chest => chest.Gold!.Value.Max));
        Assert.Equal([5, 5, 16, 23], chests.Select(chest => chest.Loot!.Count));
        Assert.Equal([0x0E43u, 0x0E41u, 0x09ABu, 0x0E40u], chests.Select(chest => chest.ItemId.Value));
    }

    [Fact]
    public async Task ShippedLootTables_LoadAgainstTheShippedItems()
    {
        var directories = Directories();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();

        var tables = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync())
            .Entities;

        Assert.Equal(126, tables.Count);
        // What the town containers fill up with: ModernUO's 35 kinds of place.
        var fillable = tables.Where(table => table.Id.StartsWith("fillable_", StringComparison.Ordinal)).ToList();
        Assert.Equal(35, fillable.Count);
        Assert.All(fillable, table => Assert.NotEmpty(table.Entries));
        Assert.Equal(338, fillable.Sum(table => table.Entries.Count));
    }

    [Fact]
    public async Task ShippedJailChestsAndNote_AreWhatTheJailNeeds()
    {
        TomlUtils.AddTomlConverter(new Point3DTomlConverter());
        var directories = Directories();
        var items = (await new ItemTemplatesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var templates = items.ToDictionary(template => template.Id);
        var loots = (await new LootTemplatesLoader(directories, new StubDataLoaderService().With(items)).LoadDataAsync())
            .Entities.ToArray();
        var tables = loots.ToDictionary(table => table.Id);
        var names = (await new NamesLoader(directories).LoadDataAsync()).Entities.ToArray();
        var mobiles = (await new MobileTemplatesLoader(
                directories,
                new StubDataLoaderService().With(names).With(items).With(loots)
            )
            .LoadDataAsync()).Entities.ToArray();
        var lists = (await new NpcListsLoader(directories, new StubDataLoaderService().With(mobiles)).LoadDataAsync())
            .Entities.ToArray();
        var spawns = (await new SpawnsLoader(directories, new StubDataLoaderService().With(mobiles).With(lists).With(items))
            .LoadDataAsync()).Entities;
        var jail = Assert.Single((await new JailLoader(directories).LoadDataAsync()).Entities);

        Assert.Equal("jail_note", templates[JailService.NoteTemplate].ScriptId);
        var chest = templates["jail_chest"];
        Assert.Equal(((bool?)false, (bool?)true, (int?)60), (chest.Movable, chest.Decays, chest.DecayMinutes));
        Assert.Equal(["jail_bread", "jail_water"], chest.Loot);
        // One entry each: a chest always has bread and water.
        Assert.Equal("0x103b_bread_loaf", Assert.Single(tables["jail_bread"].Entries).ItemId);
        Assert.Equal("0x1f9e_pitcher_of_water", Assert.Single(tables["jail_water"].Entries).ItemId);

        // One region per cell, on one tile of the cell that is not where the prisoner arrives.
        var regions = spawns.Where(spawn => spawn.ItemIds.Contains("jail_chest")).ToList();
        Assert.Equal(jail.Cell.Count, regions.Count);
        Assert.All(
            regions,
            region =>
            {
                Assert.Equal((jail.Map, 1, 1, 2), (region.Map, region.Max, region.MinMinutes, region.MaxMinutes));
                var area = Assert.Single(region.Areas);
                Assert.Equal((area.X1, area.Y1), (area.X2, area.Y2));
                Assert.InRange(area.X1, 5272, 5310);
                Assert.InRange(area.Y1, 1160, 1190);
                Assert.DoesNotContain(jail.Cell, cell => cell.Location.X == area.X1 && cell.Location.Y == area.Y1);
            }
        );
        Assert.Equal(regions.Count, regions.Select(region => (region.Areas[0].X1, region.Areas[0].Y1)).Distinct().Count());
    }

    private static bool IsVendor(MobileTemplate template, Dictionary<string, MobileTemplate> mobiles)
    {
        for (var parent = template.BaseId;
             parent is not null && mobiles.TryGetValue(parent, out var next);
             parent = next.BaseId)
        {
            if (parent == "basevendor")
            {
                return true;
            }
        }

        return false;
    }

    // The imported ModernUO catalog is the folder templates/books/modernuo: a book's id is its file name.
    private static HashSet<string> ImportedBookIds()
    {
        var folder = Path.Combine(FindRepositoryRoot(), "moongate_root", "templates", "books", "modernuo");

        return Directory.EnumerateFiles(folder, "*.toml")
            .Select(file => Path.GetFileNameWithoutExtension(file)!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static DirectoriesConfig Directories()
    {
        return new(Path.Combine(FindRepositoryRoot(), "moongate_root"), ["data", "templates"]);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Moongate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Moongate.slnx not found above the test output.");
    }
}
