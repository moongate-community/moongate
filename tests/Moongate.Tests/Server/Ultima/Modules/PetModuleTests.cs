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
