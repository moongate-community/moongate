using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Data.Mounts;
using Moongate.Server.Ultima.Data.Pets;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Taming;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Types.Pets;
using Moongate.Tests.TestSupport.Scripting;
using Moongate.Tests.TestSupport.Timing;
using Moongate.Tests.TestSupport.Ultima.Items;
using Moongate.Tests.TestSupport.Ultima.Loaders;
using Moongate.Tests.TestSupport.Ultima.Mobiles;
using Moongate.Tests.TestSupport.Ultima.Movement;
using Moongate.Tests.TestSupport.Ultima.Sectors;
using Moongate.Tests.TestSupport.Ultima.Speech;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Server.Ultima.Services;

public sealed class PetLoyaltyTests
{
    private readonly MobileService _mobiles = new(new StubMovementService(), TestSectors.Create());
    private readonly RecordingMobileStateService _state = new();
    private readonly PetsConfig _config = new();
    private double _roll = 0.5;
    private readonly PetService _service;
    private readonly StubDataLoaderService _data;

    private readonly MobileEntity _player = new()
    {
        Id = new(2), Name = "Aria", AccountId = new Serial(1002), Body = 400, Map = MapType.Felucca,
        Location = new Point3D(1000, 1000, 0)
    };

    public PetLoyaltyTests()
    {
        _data = new StubDataLoaderService().With(
            new TamingCreature { Template = "horse", MinSkill = 29.1, Slots = 1, Food = ["fruit", "grain"] },
            new TamingCreature { Template = "dog", MinSkill = 10, Slots = 1 },
            new TamingCreature { Template = "drake", MinSkill = 90, Slots = 3, Food = ["meat"] },
            new TamingCreature { Template = "hard", MinSkill = 70, Slots = 1 }
        );
        _data.With(
            new PetFood { Kind = "fruit", Items = ["apple"] },
            new PetFood { Kind = "grain", Items = ["bread"] },
            new PetFood { Kind = "meat", Items = ["ham"] }
        );
        var taming = new TamingService(_data);
        _service = new(
            _mobiles,
            TestItems.Create(),
            taming,
            _config,
            null,
            new Lazy<IMobileStateService>(() => _state),
            null,
            new PetFoodService(_data, taming),
            () => _roll
        );
        _mobiles.EnterWorld(_player);
    }

    [Fact]
    public void Loyalty_OfAPetThatHasNone_IsFull()
    {
        Assert.Equal(100, _service.Loyalty(Pet(0x100, "horse")));
    }

    [Theory]
    [InlineData(100, -10, 90)]
    [InlineData(5, -10, 0)]
    [InlineData(95, 10, 100)]
    public void AdjustLoyalty_KeepsItFromZeroToAHundred(int start, int delta, int expected)
    {
        var horse = Pet(0x100, "horse");
        horse.SetProp(MountProps.PetLoyalty, start);

        Assert.Equal(expected, _service.AdjustLoyalty(horse, delta));
        Assert.Equal(expected, _service.Loyalty(horse));
    }

    [Fact]
    public void LetGo_AnOwnedPet_IsWild_AndForgetsItsLoyalty()
    {
        var horse = Pet(0x100, "horse");
        horse.SetProp(MountProps.PetLoyalty, 3);

        Assert.True(_service.LetGo(horse));

        Assert.False(horse.TryGetProp<long>(MountProps.Owner, out _));
        Assert.False(horse.TryGetProp<int>(MountProps.PetLoyalty, out _));
        Assert.Equal(0, _service.Followers(_player));
    }

    [Fact]
    public void LetGo_AWildPet_IsRefused()
    {
        var horse = Pet(0x100, "horse");
        horse.RemoveProp(MountProps.Owner);

        Assert.False(_service.LetGo(horse));
    }

    [Theory]
    [InlineData("horse", 0, 1.0)] // asks 29.1: always obeys
    [InlineData("dog", 0, 1.0)]
    public void ControlChance_OfAnEasyCreature_IsOne(string template, int taming, double expected)
    {
        Teach(taming, 0);

        Assert.Equal(expected, _service.ControlChance(_player, Pet(0x100, template)));
    }

    [Fact]
    public void ControlChance_WithTheLoreMissing_IsPulledDownByIt()
    {
        // Taming 100.0 against a creature that asks 70.0 is +300 (x6); lore 0 is -700 (x14): (1800 - 9800) / 2 = -4000.
        Teach(1000, 0);

        Assert.Equal(0.22, _service.ControlChance(_player, Pet(0x100, "hard")), 3);
    }

    [Fact]
    public void ControlChance_WithTamingAndLoreAboveTheCreature_GrowsSixPerPoint()
    {
        // +100 over the creature in both: (600 + 600) / 2 = 600 -> 700 + 600 = 1300 -> 990.
        Teach(800, 800);

        Assert.Equal(0.99, _service.ControlChance(_player, Pet(0x100, "hard")), 3);
    }

    [Fact]
    public void ControlChance_WithTheSkillsHigh_IsTheCeiling_AndLoyaltyTakesOneTenthPerPoint()
    {
        Teach(1200, 1200);
        var hard = Pet(0x100, "hard");

        Assert.Equal(0.99, _service.ControlChance(_player, hard), 3);

        hard.SetProp(MountProps.PetLoyalty, 40);

        Assert.Equal(0.99 - 0.6, _service.ControlChance(_player, hard), 3);
    }

    [Fact]
    public void ControlChance_NeverBelowZero()
    {
        var hard = Pet(0x100, "hard");
        hard.SetProp(MountProps.PetLoyalty, 0);

        Assert.Equal(0, _service.ControlChance(_player, hard));
    }

    [Fact]
    public void Obey_WhenTheRollIsUnderTheChance_Obeys_AndGainsALoyaltyPoint()
    {
        Teach(1200, 1200);
        var hard = Pet(0x100, "hard");
        hard.SetProp(MountProps.PetLoyalty, 80);
        _roll = 0.1;

        Assert.Equal(PetObeyResultType.Obeyed, _service.Obey(_player, hard));
        Assert.Equal(81, _service.Loyalty(hard));
    }

    [Fact]
    public void Obey_WhenTheRollIsOverTheChance_Disobeys_AndLosesThree()
    {
        Teach(1200, 1200);
        var hard = Pet(0x100, "hard");
        hard.SetProp(MountProps.PetLoyalty, 80);
        _roll = 0.99;

        Assert.Equal(PetObeyResultType.Disobeyed, _service.Obey(_player, hard));
        Assert.Equal(77, _service.Loyalty(hard));
        Assert.True(hard.TryGetProp<long>(MountProps.Owner, out _));
    }

    [Fact]
    public void Obey_ADisobedienceThatEmptiesTheLoyalty_MakesThePetWild()
    {
        var hard = Pet(0x100, "hard");
        hard.SetProp(MountProps.PetLoyalty, 2);
        _roll = 0.99;

        Assert.Equal(PetObeyResultType.Wild, _service.Obey(_player, hard));
        Assert.False(hard.TryGetProp<long>(MountProps.Owner, out _));
    }

    [Fact]
    public void Obey_AnEasyCreature_AlwaysObeysAndKeepsItsLoyalty()
    {
        var dog = Pet(0x100, "dog");
        dog.SetProp(MountProps.PetLoyalty, 50);
        _roll = 0.99;

        Assert.Equal(PetObeyResultType.Obeyed, _service.Obey(_player, dog));
        Assert.Equal(50, _service.Loyalty(dog));
    }

    [Fact]
    public void Obey_SomeoneElsesPet_IsNotYours()
    {
        var hard = Pet(0x100, "hard");
        hard.SetProp(MountProps.Owner, 77L);

        Assert.Equal(PetObeyResultType.NotYours, _service.Obey(_player, hard));
    }

    [Fact]
    public void Feed_TheRightFood_RaisesTheLoyaltyByTenAnItem()
    {
        var horse = Pet(0x100, "horse");
        horse.SetProp(MountProps.PetLoyalty, 50);

        Assert.Equal(PetFeedResultType.Fed, _service.Feed(_player, horse, "apple", 3));
        Assert.Equal(80, _service.Loyalty(horse));
    }

    [Fact]
    public void Feed_APlentyOfFood_StopsAtAHundred()
    {
        var horse = Pet(0x100, "horse");
        horse.SetProp(MountProps.PetLoyalty, 50);

        Assert.Equal(PetFeedResultType.Fed, _service.Feed(_player, horse, "bread", 40));
        Assert.Equal(100, _service.Loyalty(horse));
    }

    [Fact]
    public void Feed_APetThatIsFull_EatsAllTheSame()
    {
        Assert.Equal(PetFeedResultType.AlreadyHappy, _service.Feed(_player, Pet(0x100, "horse"), "apple", 1));
    }

    [Theory]
    [InlineData("ham")] // a horse does not eat meat
    [InlineData("sword")]
    [InlineData(null)]
    public void Feed_WhatThePetDoesNotEat_IsWrongFood(string? item)
    {
        var horse = Pet(0x100, "horse");
        horse.SetProp(MountProps.PetLoyalty, 50);

        Assert.Equal(PetFeedResultType.WrongFood, _service.Feed(_player, horse, item, 1));
        Assert.Equal(50, _service.Loyalty(horse));
    }

    [Fact]
    public void Feed_SomeoneElsesPet_IsNotYours()
    {
        var horse = Pet(0x100, "horse");
        horse.SetProp(MountProps.Owner, 77L);

        Assert.Equal(PetFeedResultType.NotYours, _service.Feed(_player, horse, "apple", 1));
    }

    [Fact]
    public void Drain_TakesTheLoyaltyDrainOffEveryOwnedPet_AndOnlyThose()
    {
        var timers = new RecordingTimerService();
        var speech = new RecordingSpeechService();
        var loyalty = new PetLoyaltyService(timers, _mobiles, _service, speech, _config);
        var horse = Pet(0x100, "horse");
        var wild = Pet(0x101, "horse");
        wild.RemoveProp(MountProps.Owner);

        loyalty.Drain();

        Assert.Equal(90, _service.Loyalty(horse));
        Assert.Equal(100, _service.Loyalty(wild));
        Assert.Empty(speech.SaidClilocs);
    }

    [Fact]
    public void Drain_BelowTen_ThePetLooksAroundDesperately()
    {
        var speech = new RecordingSpeechService();
        var loyalty = new PetLoyaltyService(new RecordingTimerService(), _mobiles, _service, speech, _config);
        var horse = Pet(0x100, "horse");
        horse.SetProp(MountProps.PetLoyalty, 15);

        loyalty.Drain();

        Assert.Equal((horse, PetLoyaltyService.DesperateMessage), (speech.SaidClilocs.Single().Speaker, speech.SaidClilocs.Single().Cliloc));
        Assert.True(horse.TryGetProp<long>(MountProps.Owner, out _));
    }

    [Fact]
    public void Drain_AtZero_ThePetDecidesItIsBetterOffWithoutAMaster()
    {
        var speech = new RecordingSpeechService();
        var loyalty = new PetLoyaltyService(new RecordingTimerService(), _mobiles, _service, speech, _config);
        var horse = Pet(0x100, "horse");
        horse.SetProp(MountProps.PetLoyalty, 10);

        loyalty.Drain();

        Assert.Equal(PetLoyaltyService.WildMessage, speech.SaidClilocs.Single().Cliloc);
        Assert.False(horse.TryGetProp<long>(MountProps.Owner, out _));
    }

    [Fact]
    public async Task StartAsync_RegistersARepeatingTimerOfTheDrainMinutes()
    {
        var timers = new RecordingTimerService();
        _config.LoyaltyDrainMinutes = 30;

        await new PetLoyaltyService(timers, _mobiles, _service, new RecordingSpeechService(), _config).StartAsync();

        var timer = Assert.Single(timers.Timers);
        Assert.Equal(PetLoyaltyService.TimerName, timer.Name);
        Assert.Equal(TimeSpan.FromMinutes(30), timer.Interval);
    }

    private void Teach(int taming, int lore)
    {
        _state.Skills.Add(new MobileSkill { Skill = SkillType.AnimalTaming, Base = taming });
        _state.Skills.Add(new MobileSkill { Skill = SkillType.AnimalLore, Base = lore });
    }

    private MobileEntity Pet(uint serial, string template)
    {
        var creature = new MobileEntity
        {
            Id = new(serial), Name = template, TemplateId = template, Map = MapType.Felucca,
            Location = new Point3D(1001, 1000, 0)
        };
        creature.SetProp(MountProps.Owner, (long)_player.Id.Value);
        _mobiles.EnterWorld(creature);

        return creature;
    }
}
