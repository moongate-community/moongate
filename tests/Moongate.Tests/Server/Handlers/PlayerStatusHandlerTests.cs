using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Player;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Characters;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Handlers;

public class PlayerStatusHandlerTests
{
    [SetUp]
    public void SetUp()
    {
        SkillInfo.Table =
        [
            new(0, "Alchemy", 0, 0, 100, "Alchemist", 0, 0, 0, 1, "Alchemy", Stat.Intelligence, Stat.Intelligence),
            new(25, "Magery", 0, 0, 100, "Wizard", 0, 0, 0, 1, "Magery", Stat.Intelligence, Stat.Intelligence)
        ];
    }

    private sealed class TestCharacterService : ICharacterService
    {
        public Task<bool> AddCharacterToAccountAsync(Serial accountId, Serial characterId)
        {
            _ = accountId;
            _ = characterId;

            return Task.FromResult(true);
        }

        public Task ApplyStarterEquipmentHuesAsync(Serial characterId, short shirtHue, short pantsHue)
        {
            _ = characterId;
            _ = shirtHue;
            _ = pantsHue;

            return Task.CompletedTask;
        }

        public Task<Serial> CreateCharacterAsync(UOMobileEntity character)
        {
            _ = character;

            return Task.FromResult((Serial)1u);
        }

        public Task<UOItemEntity?> GetBackpackWithItemsAsync(UOMobileEntity character)
        {
            _ = character;

            return Task.FromResult<UOItemEntity?>(null);
        }

        public Task<UOItemEntity?> GetBankBoxWithItemsAsync(UOMobileEntity character)
        {
            _ = character;

            return Task.FromResult<UOItemEntity?>(null);
        }

        public Task<UOMobileEntity?> GetCharacterAsync(Serial characterId)
        {
            var mobile = new UOMobileEntity
            {
                Id = characterId,
                Name = "Loaded",
                Hits = 50,
                MaxHits = 100
            };
            mobile.InitializeSkills();
            mobile.SetSkill(UOSkillName.Magery, 500, lockState: UOSkillLock.Locked);

            return Task.FromResult<UOMobileEntity?>(mobile);
        }

        public Task<List<UOMobileEntity>> GetCharactersForAccountAsync(Serial accountId)
        {
            _ = accountId;

            return Task.FromResult(new List<UOMobileEntity>());
        }

        public Task<bool> RemoveCharacterFromAccountAsync(Serial accountId, Serial characterId)
        {
            _ = accountId;
            _ = characterId;

            return Task.FromResult(true);
        }
    }

    [Test]
    public async Task HandlePacketAsync_ShouldEnqueueStatusPacket_WhenBasicStatusRequested()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var characterService = new TestCharacterService();
        var handler = new PlayerStatusHandler(queue, characterService);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x00000002,
            Name = "Tommy",
            Hits = 80,
            MaxHits = 100
        };
        var session = new GameSession(new(client))
        {
            CharacterId = mobile.Id,
            Character = mobile
        };

        var packet = new GetPlayerStatusPacket
        {
            StatusType = GetPlayerStatusType.BasicStatus,
            MobileSerial = mobile.Id.Value
        };

        var handled = await handler.HandlePacketAsync(session, packet);
        var dequeued = queue.TryDequeue(out var outbound);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(dequeued, Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<PlayerStatusPacket>());
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_ShouldEnqueueSkillListPacket_WhenRequestSkillsTypeRequested()
    {
        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var characterService = new TestCharacterService();
        var handler = new PlayerStatusHandler(queue, characterService);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000002,
            Character = new()
            {
                Id = (Serial)0x00000002,
                Name = "Tommy",
                Hits = 80,
                MaxHits = 100
            }
        };
        session.Character.InitializeSkills();
        session.Character.SetSkill(UOSkillName.Alchemy, 250, lockState: UOSkillLock.Down);

        var packet = new GetPlayerStatusPacket
        {
            StatusType = GetPlayerStatusType.RequestSkills,
            MobileSerial = 0x00000002
        };

        var handled = await handler.HandlePacketAsync(session, packet);
        var dequeued = queue.TryDequeue(out var outbound);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(dequeued, Is.True);
                Assert.That(outbound.Packet, Is.TypeOf<SkillListPacket>());
            }
        );
    }
}
