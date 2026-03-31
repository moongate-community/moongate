using MemoryPack;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;
using Moongate.Tests.Persistence.Support;

namespace Moongate.Tests.Persistence;

public sealed class EntityMemoryPackRoundTripTests
{
    [Test]
    public void AccountEntity_ShouldRoundTripDirectlyThroughMemoryPack()
    {
        var entity = new UOAccountEntity
        {
            Id = (Serial)0x00000033u,
            Username = "admin",
            PasswordHash = "hash",
            CreatedUtc = new(2026, 3, 20, 8, 0, 0, DateTimeKind.Utc),
            LastLoginUtc = new(2026, 3, 20, 8, 5, 0, DateTimeKind.Utc),
            CharacterIds = [(Serial)0x100u, (Serial)0x101u],
            AccountType = AccountType.Administrator,
            Email = "admin@test.local",
            IsLocked = false,
            ActivationId = "act",
            RecoveryCode = "rec"
        };

        var payload = MemoryPackSerializer.Serialize(entity);
        var restored = MemoryPackSerializer.Deserialize<UOAccountEntity>(payload);

        Assert.That(restored, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(restored!.Id, Is.EqualTo(entity.Id));
                Assert.That(restored.Username, Is.EqualTo(entity.Username));
                Assert.That(restored.CharacterIds, Is.EqualTo(entity.CharacterIds));
                Assert.That(restored.AccountType, Is.EqualTo(entity.AccountType));
            }
        );
    }

    [Test]
    public void BulletinBoardMessageEntity_ShouldRoundTripDirectlyThroughMemoryPack()
    {
        var entity = new BulletinBoardMessageEntity
        {
            MessageId = (Serial)0x40000091u,
            BoardId = (Serial)0x40000055u,
            ParentId = (Serial)0x40000011u,
            OwnerCharacterId = (Serial)0x00000077u,
            Author = "The Poster",
            Subject = "Test Subject",
            PostedAtUtc = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc)
        };
        entity.BodyLines.AddRange(["line one", "line two"]);

        var payload = MemoryPackSerializer.Serialize(entity);
        var restored = MemoryPackSerializer.Deserialize<BulletinBoardMessageEntity>(payload);

        Assert.That(restored, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(restored!.MessageId, Is.EqualTo(entity.MessageId));
                Assert.That(restored.Author, Is.EqualTo(entity.Author));
                Assert.That(restored.BodyLines, Is.EqualTo(entity.BodyLines));
            }
        );
    }

    [Test]
    public void HelpTicketEntity_ShouldRoundTripDirectlyThroughMemoryPack()
    {
        var entity = new HelpTicketEntity
        {
            Id = (Serial)0x4000004Bu,
            SenderCharacterId = (Serial)0x00000042u,
            SenderAccountId = (Serial)0x00000024u,
            Category = HelpTicketCategory.Question,
            Message = "Stuck near the docks.",
            MapId = 0,
            Location = new(1443, 1692, 0),
            Status = HelpTicketStatus.Open,
            AssignedToCharacterId = (Serial)0x00000003u,
            AssignedToAccountId = (Serial)0x00000002u,
            CreatedAtUtc = new(2026, 3, 20, 9, 30, 0, DateTimeKind.Utc),
            AssignedAtUtc = new(2026, 3, 20, 9, 45, 0, DateTimeKind.Utc),
            ClosedAtUtc = null,
            LastUpdatedAtUtc = new(2026, 3, 20, 9, 45, 0, DateTimeKind.Utc)
        };

        var payload = MemoryPackSerializer.Serialize(entity);
        var restored = MemoryPackSerializer.Deserialize<HelpTicketEntity>(payload);

        Assert.That(restored, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(restored!.Id, Is.EqualTo(entity.Id));
                Assert.That(restored.Location, Is.EqualTo(entity.Location));
                Assert.That(restored.Message, Is.EqualTo(entity.Message));
                Assert.That(restored.Status, Is.EqualTo(entity.Status));
            }
        );
    }

    [Test]
    public void ItemEntity_ShouldRoundTripDirectlyThroughMemoryPack()
    {
        var entity = new UOItemEntity
        {
            Id = (Serial)0x40000010u,
            Name = "typed-item",
            Location = new(10, 20, 0),
            MapId = 0,
            ItemId = 0x13B9,
            Amount = 3,
            WeaponSkill = UOSkillName.Archery,
            AmmoItemId = 0x0F3F,
            AmmoEffectId = 0x1BFE,
            IsQuiver = true,
            QuiverLowerAmmoCost = 20,
            QuiverDamageIncrease = 10,
            QuiverWeightReduction = 30,
            CombatStats = new()
            {
                MinStrength = 40,
                DamageMin = 11,
                DamageMax = 13,
                Defense = 15
            },
            Modifiers = new()
            {
                StrengthBonus = 5,
                PhysicalResist = 12,
                Luck = 100
            }
        };
        entity.SetCustomProperty(
            "crafted_by",
            new()
            {
                Type = ItemCustomPropertyType.String,
                StringValue = "The Blacksmith"
            }
        );

        var payload = MemoryPackSerializer.Serialize(entity);
        var restored = MemoryPackSerializer.Deserialize<UOItemEntity>(payload);

        Assert.That(restored, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(restored!.Id, Is.EqualTo(entity.Id));
                Assert.That(restored.Location, Is.EqualTo(entity.Location));
                Assert.That(restored.Amount, Is.EqualTo(entity.Amount));
                Assert.That(restored.WeaponSkill, Is.EqualTo(entity.WeaponSkill));
                Assert.That(restored.AmmoItemId, Is.EqualTo(entity.AmmoItemId));
                Assert.That(restored.AmmoEffectId, Is.EqualTo(entity.AmmoEffectId));
                Assert.That(restored.IsQuiver, Is.EqualTo(entity.IsQuiver));
                Assert.That(restored.QuiverLowerAmmoCost, Is.EqualTo(entity.QuiverLowerAmmoCost));
                Assert.That(restored.QuiverDamageIncrease, Is.EqualTo(entity.QuiverDamageIncrease));
                Assert.That(restored.QuiverWeightReduction, Is.EqualTo(entity.QuiverWeightReduction));
                Assert.That(restored.CustomProperties["crafted_by"].StringValue, Is.EqualTo("The Blacksmith"));
            }
        );
    }

    [Test]
    public void MobileEntity_ShouldRoundTripDirectlyThroughMemoryPack()
    {
        SkillInfo.Table =
        [
            new(
                0,
                "Alchemy",
                0,
                0,
                100,
                "Alchemist",
                0,
                0,
                0,
                1,
                "Alchemy",
                Stat.Intelligence,
                Stat.Intelligence
            ),
            new(
                1,
                "Anatomy",
                0,
                0,
                100,
                "Anatomist",
                0,
                0,
                0,
                1,
                "Anatomy",
                Stat.Strength,
                Stat.Intelligence
            )
        ];

        var entity = new UOMobileEntity
        {
            Id = (Serial)0x100u,
            AccountId = (Serial)0x33u,
            Name = "test-mobile",
            FactionId = "true_britannians",
            Location = new(100, 200, 0),
            MapId = 0,
            Gender = GenderType.Male,
            BaseStats = new()
            {
                Strength = 50,
                Dexterity = 40,
                Intelligence = 30
            }
        };
        entity.EquippedItemIds[ItemLayerType.OneHanded] = (Serial)0x40000020u;
        entity.Sounds[MobileSoundType.Attack] = 0x023B;
        entity.SetCustomProperty(
            "test_key",
            new()
            {
                Type = ItemCustomPropertyType.Integer,
                IntegerValue = 42
            }
        );
        entity.SetSkill(UOSkillName.Alchemy, 500, 500);

        var payload = MemoryPackSerializer.Serialize(entity);
        var restored = MemoryPackSerializer.Deserialize<UOMobileEntity>(payload);

        Assert.That(restored, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(restored!.Id, Is.EqualTo(entity.Id));
                Assert.That(restored.FactionId, Is.EqualTo("true_britannians"));
                Assert.That(restored.Location, Is.EqualTo(entity.Location));
                Assert.That(restored.BaseStats.Strength, Is.EqualTo(50));
                Assert.That(restored.EquippedItemIds[ItemLayerType.OneHanded], Is.EqualTo((Serial)0x40000020u));
                Assert.That(restored.Sounds[MobileSoundType.Attack], Is.EqualTo(0x023B));
                Assert.That(restored.CustomProperties["test_key"].IntegerValue, Is.EqualTo(42));
                Assert.That(restored.Skills[UOSkillName.Alchemy].Value, Is.EqualTo(500));
            }
        );
    }

    [Test]
    public void MobileEntity_QuestProgress_ShouldRoundTripDirectlyThroughMemoryPack()
    {
        var entity = new UOMobileEntity
        {
            Id = (Serial)0x101u
        };
        entity.QuestProgress.Add(
            new()
            {
                QuestId = "quest::find-the-herb",
                Status = QuestProgressStatusType.Completed,
                AcceptedAtUtc = new(2026, 3, 31, 9, 15, 0, DateTimeKind.Utc),
                CompletedAtUtc = new(2026, 3, 31, 10, 30, 0, DateTimeKind.Utc),
                Objectives =
                [
                    new()
                    {
                        ObjectiveIndex = 0,
                        CurrentAmount = 3,
                        IsCompleted = true,
                        ObjectiveId = "kill:3:sewer_rat"
                    },
                    new()
                    {
                        ObjectiveIndex = 1,
                        CurrentAmount = 1,
                        IsCompleted = true,
                        ObjectiveId = "collect:1:rat_tail"
                    }
                ]
            }
        );

        var payload = MemoryPackSerializer.Serialize(entity);
        var restored = MemoryPackSerializer.Deserialize<UOMobileEntity>(payload);

        Assert.That(restored, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(restored!.QuestProgress, Has.Count.EqualTo(1));
                Assert.That(restored.QuestProgress[0].QuestId, Is.EqualTo("quest::find-the-herb"));
                Assert.That(restored.QuestProgress[0].Status, Is.EqualTo(QuestProgressStatusType.Completed));
                Assert.That(restored.QuestProgress[0].AcceptedAtUtc, Is.EqualTo(entity.QuestProgress[0].AcceptedAtUtc));
                Assert.That(restored.QuestProgress[0].CompletedAtUtc, Is.EqualTo(entity.QuestProgress[0].CompletedAtUtc));
                Assert.That(restored.QuestProgress[0].Objectives, Has.Count.EqualTo(2));
                Assert.That(restored.QuestProgress[0].Objectives[0].ObjectiveIndex, Is.EqualTo(0));
                Assert.That(restored.QuestProgress[0].Objectives[0].CurrentAmount, Is.EqualTo(3));
                Assert.That(restored.QuestProgress[0].Objectives[0].ObjectiveId, Is.EqualTo("kill:3:sewer_rat"));
                Assert.That(restored.QuestProgress[0].Objectives[1].IsCompleted, Is.True);
                Assert.That(restored.QuestProgress[0].Objectives[1].ObjectiveIndex, Is.EqualTo(1));
                Assert.That(restored.QuestProgress[0].Objectives[1].CurrentAmount, Is.EqualTo(1));
                Assert.That(restored.QuestProgress[0].Objectives[1].ObjectiveId, Is.EqualTo("collect:1:rat_tail"));
            }
        );
    }

    [Test]
    public void MobileEntity_LegacyPayloadWithoutQuestProgress_ShouldDeserializeWithEmptyQuestProgress()
    {
        var legacy = new UOMobileEntityLegacyWithoutQuestProgress
        {
            Id = (Serial)0x102u
        };

        var payload = MemoryPackSerializer.Serialize(legacy);
        var restored = MemoryPackSerializer.Deserialize<UOMobileEntity>(payload);

        Assert.That(restored, Is.Not.Null);
        Assert.Multiple(
            () =>
            {
                Assert.That(restored!.Id, Is.EqualTo(legacy.Id));
                Assert.That(restored.QuestProgress, Is.Not.Null);
                Assert.That(restored.QuestProgress, Is.Empty);
            }
        );
    }
}
