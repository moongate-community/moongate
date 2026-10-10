using Lua;
using Lua.Standard;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Binding;
using Moongate.Scripting.Internal;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Data.Taming;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Modules;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Pets;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Pets;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Modules;

public sealed class PetModuleTests : IAsyncLifetime
{
    private readonly StubPetService _pets = new();
    private readonly RecordingMobileStateService _state = new();

    private readonly TamingService _taming = new(
        new StubDataLoaderService().With(new TamingCreature { Template = "horse", MinSkill = 29.1, Slots = 2 })
    );

    private readonly SettableClock _clock = new();
    private PetModule? _module;
    private BroadcastFixture _fixture = null!;
    private GameSession _session = null!;
    private MobileEntity _aria = null!;

    private readonly MobileEntity _horse = new()
    {
        Id = new Serial(0x100), Name = "a horse", TemplateId = "horse", Map = MapType.Trammel,
        Location = new Point3D(1601, 1600, 0)
    };

    private readonly MobileEntity _orc = new()
    {
        Id = new Serial(0x101), Name = "an orc", TemplateId = "orc", Map = MapType.Trammel,
        Location = new Point3D(1602, 1600, 0)
    };

    public async Task InitializeAsync()
    {
        _fixture = await BroadcastFixture.CreateAsync();
        _session = await _fixture.AddAsync(2);
        Assert.True(_fixture.Mobiles.TryGet(new Serial(2), out _aria!));
        _aria.AccountId = new Serial(0x42);
        _fixture.Mobiles.EnterWorld(_horse);
        _fixture.Mobiles.EnterWorld(_orc);
    }

    [Fact]
    public void Info_OfATamableCreature_GivesItsSkillItsSlotsAndItsOwner()
    {
        _horse.SetProp(MountProps.Owner, 77L);

        var result = Run("local i = pet.info(0x100) return i.min_skill, i.slots, i.owner");

        Assert.Equal([29.1, 2.0, 77.0], result.Select(value => value.Read<double>()));
    }

    [Fact]
    public void Info_OfAWildCreature_HasNoOwner()
    {
        Assert.Equal(0.0, Run("return pet.info(0x100).owner")[0].Read<double>());
    }

    [Fact]
    public void Info_OfWhatCannotBeTamed_IsNil()
    {
        var result = Run("return pet.info(0x101), pet.info(2), pet.info(0x999)");

        Assert.All(result, value => Assert.Equal(LuaValue.Nil, value));
    }

    [Fact]
    public void Followers_AndMaxFollowers_AreThoseOfThePetService()
    {
        _pets.FollowerCount = 3;
        _pets.MaxFollowers = 8;

        var result = Run("return pet.followers(2), pet.max_followers(), pet.followers(0x100), pet.followers(0x999)");

        Assert.Equal([3, 8, 0, 0], result.Select(value => (int)value.Read<double>()));
    }

    [Fact]
    public void SlotsOf_IsWhatTheTemplateCountsFor_AndOneForANameThatIsNone()
    {
        _pets.Slots["airele_summon"] = 2;

        var result = Run("return pet.slots_of('airele_summon'), pet.slots_of('cat')");

        Assert.Equal([2, 1], result.Select(value => (int)value.Read<double>()));
    }

    [Fact]
    public void Refresh_TellsThePetServiceTheFollowersChanged_ForAPlayerOnly()
    {
        var result = Run("return pet.refresh(2), pet.refresh(0x100), pet.refresh(0x999)");

        Assert.Equal([true, false, false], result.Select(value => value.Read<bool>()));
        Assert.Equal([_aria.Id], _pets.ChangedFor);
    }

    [Fact]
    public void Release_AsksThePetService()
    {
        Assert.True(Run("return pet.release(2, 0x100)")[0].Read<bool>());
        Assert.Equal((_aria, _horse), Assert.Single(_pets.Released));

        _pets.Releases = false;

        Assert.False(Run("return pet.release(2, 0x100)")[0].Read<bool>());
        Assert.False(Run("return pet.release(0x999, 0x100)")[0].Read<bool>());
        Assert.False(Run("return pet.release(2, 0x999)")[0].Read<bool>());
    }

    [Fact]
    public void Attend_TheFirstPetThatAsksAnswersForAll_UntilAMomentHasPassed_AndAnNpcIsNeverServed()
    {
        var result = Run("return pet.attend(2), pet.attend(2), pet.attend(0x100), pet.attend(0x999)");

        Assert.Equal([true, false, false, false], result.Select(value => value.Read<bool>()));

        _clock.Advance(TimeSpan.FromSeconds(1));

        Assert.True(Run("return pet.attend(2)")[0].Read<bool>());
    }

    [Fact]
    public void Tame_AsksThePetService_AndOnSuccessShowsThePlayerItsStatus()
    {
        var result = Run("return pet.tame(2, 0x100), PetResultType.Ok");

        Assert.Equal(result[1].Read<double>(), result[0].Read<double>());
        Assert.Equal((_aria, _horse), Assert.Single(_pets.Tames));
        Assert.Equal((_session, _aria), Assert.Single(_state.Statuses));
    }

    [Fact]
    public void Tame_WhenTheServiceRefuses_ShowsNoStatus_AndAnswersTheReason()
    {
        _pets.Result = PetResultType.TooManyFollowers;

        var result = Run("return pet.tame(2, 0x100), PetResultType.TooManyFollowers");

        Assert.Equal(result[1].Read<double>(), result[0].Read<double>());
        Assert.Empty(_state.Statuses);
    }

    [Fact]
    public void Tame_ANobodyOrAnUnknownCreature_IsRefusedWithoutAskingTheService()
    {
        var result = Run("return pet.tame(0x999, 0x100), pet.tame(2, 0x999)");

        Assert.Equal(
            [(double)PetResultType.NoPlayer, (double)PetResultType.NotAnNpc],
            result.Select(value => value.Read<double>())
        );
        Assert.Empty(_pets.Tames);
    }

    [Fact]
    public void Loyalty_OfAnOwnedCreature_IsWhatThePetServiceSays_AndNilForAWildOne()
    {
        _horse.SetProp(MountProps.Owner, 2L);
        _pets.LoyaltyOf = 64;

        var result = Run("return pet.loyalty(0x100), pet.loyalty(0x101), pet.loyalty(2)");

        Assert.Equal(64.0, result[0].Read<double>());
        Assert.Equal(LuaValue.Nil, result[1]);
        Assert.Equal(LuaValue.Nil, result[2]);
    }

    [Fact]
    public void ControlChance_IsThatOfThePetService_AndZeroForNobody()
    {
        _pets.Chance = 0.42;

        var result = Run("return pet.control_chance(2, 0x100), pet.control_chance(0x999, 0x100), pet.control_chance(2, 0x999)");

        Assert.Equal([0.42, 0, 0], result.Select(value => value.Read<double>()));
    }

    [Fact]
    public void Obey_AsksThePetService_AndAnswersItsResult()
    {
        _pets.ObeyResult = PetObeyResultType.Disobeyed;

        var result = Run("return pet.obey(2, 0x100), pet.obey(0x999, 0x100)");

        Assert.Equal(
            [(double)PetObeyResultType.Disobeyed, (double)PetObeyResultType.NotYours],
            result.Select(value => value.Read<double>())
        );
        Assert.Equal((_aria, _horse), Assert.Single(_pets.Obeys));
    }

    [Fact]
    public void Feed_GivesTheWholeStackAsFood_AndTakesItAway()
    {
        var items = Moongate.Tests.TestSupport.Ultima.Items.TestItems.Create();
        var food = new ItemEntity { Id = new Serial(0x40000700), TemplateId = "apple", Amount = 4 };
        items.Add([food]);
        var handling = new Moongate.Tests.TestSupport.Ultima.Items.StubItemHandlingService();
        _module = new(_pets, _taming, _fixture.Mobiles, _state, _fixture.Sessions, _clock, items, handling);

        var result = Run("return pet.feed(2, 0x100, 0x40000700)");

        Assert.Equal((double)PetFeedResultType.Fed, result[0].Read<double>());
        Assert.Equal(("apple", 4), Assert.Single(_pets.Feeds));
        Assert.Equal(food, Assert.Single(handling.Deleted));
    }

    [Fact]
    public void Feed_WhatThePetRefuses_IsNotTakenAway()
    {
        var items = Moongate.Tests.TestSupport.Ultima.Items.TestItems.Create();
        items.Add([new ItemEntity { Id = new Serial(0x40000700), TemplateId = "sword", Amount = 1 }]);
        var handling = new Moongate.Tests.TestSupport.Ultima.Items.StubItemHandlingService();
        _module = new(_pets, _taming, _fixture.Mobiles, _state, _fixture.Sessions, _clock, items, handling);
        _pets.FeedResult = PetFeedResultType.WrongFood;

        var result = Run("return pet.feed(2, 0x100, 0x40000700), pet.feed(2, 0x100, 0x999)");

        Assert.Equal(
            [(double)PetFeedResultType.WrongFood, (double)PetFeedResultType.NotYours],
            result.Select(value => value.Read<double>())
        );
        Assert.Empty(handling.Deleted);
    }

    [Fact]
    public void Feed_WhenTheFoodCannotBeTaken_TheLoyaltyItGaveGoesBack()
    {
        var items = Moongate.Tests.TestSupport.Ultima.Items.TestItems.Create();
        items.Add([new ItemEntity { Id = new Serial(0x40000700), TemplateId = "apple", Amount = 1 }]);
        var handling = new Moongate.Tests.TestSupport.Ultima.Items.StubItemHandlingService { DeleteFails = true };
        _module = new(_pets, _taming, _fixture.Mobiles, _state, _fixture.Sessions, _clock, items, handling);
        _pets.LoyaltyOf = 50;
        _pets.FeedResult = PetFeedResultType.Fed;
        _pets.OnFeed = () => _pets.LoyaltyOf = 90;

        var result = Run("return pet.feed(2, 0x100, 0x40000700)");

        Assert.Equal((double)PetFeedResultType.WrongFood, result[0].Read<double>());
        Assert.Equal(50, _pets.LoyaltyOf);
    }

    [Fact]
    public void Feed_WhenTheFoodCannotBeTaken_TheBondItStartedGoesBackToo()
    {
        var items = Moongate.Tests.TestSupport.Ultima.Items.TestItems.Create();
        items.Add([new ItemEntity { Id = new Serial(0x40000700), TemplateId = "apple", Amount = 1 }]);
        var handling = new Moongate.Tests.TestSupport.Ultima.Items.StubItemHandlingService { DeleteFails = true };
        _module = new(_pets, _taming, _fixture.Mobiles, _state, _fixture.Sessions, _clock, items, handling);
        _pets.OnFeed = () =>
        {
            _horse.SetProp(MountProps.PetBonded, true);
            _horse.SetProp(MountProps.PetBondBegin, 99L);
        };

        Run("return pet.feed(2, 0x100, 0x40000700)");

        Assert.False(_horse.TryGetProp<bool>(MountProps.PetBonded, out _));
        Assert.False(_horse.TryGetProp<long>(MountProps.PetBondBegin, out _));
    }

    [Fact]
    public void Corpse_OfAnItemThatIsNoCorpseOrHoldsASpoiledOwner_IsNil()
    {
        var items = Moongate.Tests.TestSupport.Ultima.Items.TestItems.Create();
        var sword = new ItemEntity { Id = new Serial(0x40000810), TemplateId = "sword", ItemId = 0x0F5E };
        sword.SetProp("corpse.pet_owner", 2L);
        var spoiled = new ItemEntity { Id = new Serial(0x40000811), TemplateId = "corpse", ItemId = 0x2006 };
        spoiled.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));
        spoiled.SetProp("corpse.pet_owner", "two");
        items.Add([sword, spoiled]);
        _module = new(_pets, _taming, _fixture.Mobiles, _state, _fixture.Sessions, _clock, items);

        var result = Run("return pet.corpse(0x40000810), pet.corpse(0x40000811)");

        Assert.All(result, value => Assert.Equal(LuaValue.Nil, value));
    }

    [Fact]
    public void Feed_FoodInSomeoneElsesPack_IsNotTheirsToGive()
    {
        var items = Moongate.Tests.TestSupport.Ultima.Items.TestItems.Create();
        var pack = new ItemEntity { Id = new Serial(0x40000800), TemplateId = "backpack", MobileId = _orc.Id };
        var food = new ItemEntity { Id = new Serial(0x40000700), TemplateId = "apple", Amount = 1, ContainerId = pack.Id };
        items.Add([pack, food]);
        var handling = new Moongate.Tests.TestSupport.Ultima.Items.StubItemHandlingService();
        _module = new(_pets, _taming, _fixture.Mobiles, _state, _fixture.Sessions, _clock, items, handling);

        var result = Run("return pet.feed(2, 0x100, 0x40000700)");

        Assert.Equal((double)PetFeedResultType.NotYours, result[0].Read<double>());
        Assert.Empty(_pets.Feeds);
        Assert.Empty(handling.Deleted);
    }

    [Fact]
    public void Lore_OfATamedCreature_GivesItsArmorDamageFoodLoyaltyAndTamingData()
    {
        var templates = new Moongate.Server.Ultima.Services.MobileTemplateService(
            new StubDataLoaderService().With(
                new Moongate.Server.Ultima.Data.Templates.Mobiles.MobileTemplate
                {
                    Id = "horse", Damage = Moongate.Core.Primitives.DiceSpec.Parse("1d6+2")
                }
            )
        );
        _module = new(_pets, _taming, _fixture.Mobiles, _state, _fixture.Sessions, _clock, templates: templates);
        _horse.Armor = 6;
        _horse.SetProp(MountProps.Owner, 2L);
        _pets.LoyaltyOf = 70;

        var result = Run(
            "local l = pet.lore(0x100) return l.armor, l.damage_min, l.damage_max, l.loyalty, l.min_skill, l.slots, l.owner, #l.foods, l.foods[1]"
        );

        Assert.Equal([6.0, 3.0, 8.0, 70.0, 29.1, 2.0, 2.0, 1.0], result.Take(8).Select(value => value.Read<double>()));
        Assert.Equal("meat", result[8].Read<string>());
    }

    [Fact]
    public void Lore_OfAWildCreatureThatCannotBeTamed_HasNoLoyaltyNoSkillAndNoFood()
    {
        var result = Run("local l = pet.lore(0x101) return l.loyalty, l.min_skill, #l.foods, l.owner, l.damage_max");

        Assert.Equal(LuaValue.Nil, result[0]);
        Assert.Equal(LuaValue.Nil, result[1]);
        Assert.Equal([0.0, 0.0, 0.0], result.Skip(2).Select(value => value.Read<double>()));
    }

    [Fact]
    public void Lore_OfAPlayerOrNobody_IsNil()
    {
        var result = Run("return pet.lore(2), pet.lore(0x999)");

        Assert.All(result, value => Assert.Equal(LuaValue.Nil, value));
    }

    [Fact]
    public void Feed_ThatBondsThePet_TakesTheFoodAway()
    {
        var items = Moongate.Tests.TestSupport.Ultima.Items.TestItems.Create();
        var food = new ItemEntity { Id = new Serial(0x40000700), TemplateId = "apple", Amount = 2 };
        items.Add([food]);
        var handling = new Moongate.Tests.TestSupport.Ultima.Items.StubItemHandlingService();
        _module = new(_pets, _taming, _fixture.Mobiles, _state, _fixture.Sessions, _clock, items, handling);
        _pets.FeedResult = PetFeedResultType.Bonded;

        var result = Run("return pet.feed(2, 0x100, 0x40000700)");

        Assert.Equal((double)PetFeedResultType.Bonded, result[0].Read<double>());
        Assert.Equal(food, Assert.Single(handling.Deleted));
    }

    [Fact]
    public void Lore_SaysWhetherTheOwnedCreatureIsBonded()
    {
        _horse.SetProp(MountProps.Owner, 2L);
        _pets.Bonded = true;

        var result = Run("return pet.lore(0x100).bonded, pet.lore(0x101).bonded");

        Assert.True(result[0].Read<bool>());
        Assert.False(result[1].Read<bool>());
    }

    [Fact]
    public void Corpse_OfABondedPet_GivesItsOwnerAndWhetherItFits()
    {
        var items = Moongate.Tests.TestSupport.Ultima.Items.TestItems.Create();
        var corpse = new ItemEntity { Id = new Serial(0x40000800), TemplateId = "corpse", ItemId = 0x2006 };
        corpse.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));
        corpse.SetProp("corpse.pet_owner", 2L);
        corpse.SetProp("corpse.template", "horse");
        var plain = new ItemEntity { Id = new Serial(0x40000801), TemplateId = "corpse", ItemId = 0x2006 };
        plain.PlaceOnGround(MapType.Trammel, new Point3D(1600, 1600, 0));
        items.Add([corpse, plain]);
        _module = new(_pets, _taming, _fixture.Mobiles, _state, _fixture.Sessions, _clock, items);
        _pets.FollowerCount = 3;

        var fits = Run("local c = pet.corpse(0x40000800) return c.owner, c.fits, pet.corpse(0x40000801), pet.corpse(0x999)");

        Assert.Equal(2.0, fits[0].Read<double>());
        Assert.True(fits[1].Read<bool>());
        Assert.Equal(LuaValue.Nil, fits[2]);
        Assert.Equal(LuaValue.Nil, fits[3]);

        _pets.FollowerCount = 5;

        Assert.False(Run("return pet.corpse(0x40000800).fits")[0].Read<bool>());
    }

    private LuaValue[] Run(string chunk)
    {
        using var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.Environment["PetResultType"] = new LuaTable
        {
            ["Ok"] = (double)PetResultType.Ok, ["TooManyFollowers"] = (double)PetResultType.TooManyFollowers
        };
        state.Environment["PetObeyResultType"] = new LuaTable
        {
            ["Obeyed"] = (double)PetObeyResultType.Obeyed, ["Disobeyed"] = (double)PetObeyResultType.Disobeyed,
            ["NotYours"] = (double)PetObeyResultType.NotYours
        };
        state.Environment["PetFeedResultType"] = new LuaTable
        {
            ["Fed"] = (double)PetFeedResultType.Fed, ["WrongFood"] = (double)PetFeedResultType.WrongFood,
            ["NotYours"] = (double)PetFeedResultType.NotYours
        };
        new LuaModuleBinder(NoThreadGuard.Instance).Bind(
            state,
            _module ??= new PetModule(_pets, _taming, _fixture.Mobiles, _state, _fixture.Sessions, _clock)
        );

        return SyncValueTask.Run(state.DoStringAsync(chunk, "t"));
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }
}
