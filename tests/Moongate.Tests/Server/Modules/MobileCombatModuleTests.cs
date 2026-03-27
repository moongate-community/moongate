using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Modules;
using Moongate.Server.Services.Interaction;
using Moongate.Tests.Server.Modules.Support;
using Moongate.Tests.Server.Services.Packets;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;
using Stat = Moongate.UO.Data.Types.Stat;

namespace Moongate.Tests.Server.Modules;

public class MobileCombatModuleTests
{
    [SetUp]
    public void SetUp()
        => SkillInfo.Table =
        [
            new(
                (int)UOSkillName.Archery,
                "Archery",
                0,
                100,
                0,
                "Archer",
                0,
                0,
                0,
                1,
                "Archery",
                Stat.Dexterity,
                Stat.Strength
            )
        ];

    [Test]
    public void CheckSkill_WhenSkillIsWithinRange_ShouldReturnSuccessAndApplyGain()
    {
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x211,
            Name = "Archer",
            IsPlayer = true,
            MapId = 1,
            Location = new(100, 100, 0)
        };
        mobile.InitializeSkills();
        mobile.SetSkill(UOSkillName.Archery, 500);

        ISkillGainService skillGainService = new SkillGainService(() => 0.0);
        var module = new MobileCombatModule(
            mobileService: null,
            gameNetworkSessionService: new FakeGameNetworkSessionService(),
            spatialWorldService: new RegionDataLoaderTestSpatialWorldService(),
            outgoingPacketQueue: null,
            skillGainService: skillGainService,
            skillCheckRollProvider: () => 0.0
        );

        var succeeded = module.CheckSkill(mobile, "archery", 0.0, 100.0, 0x999);

        Assert.Multiple(
            () =>
            {
                Assert.That(succeeded, Is.True);
                Assert.That(mobile.GetSkill(UOSkillName.Archery)!.Base, Is.EqualTo(501));
            }
        );
    }

    [Test]
    public void Dismount_ShouldDelegateToMobileService()
    {
        var mobileService = new MobileModuleTestMobileService();
        var module = new MobileCombatModule(
            mobileService,
            new FakeGameNetworkSessionService(),
            new RegionDataLoaderTestSpatialWorldService(),
            null,
            null
        );

        var dismounted = module.Dismount((Serial)0x200);

        Assert.Multiple(
            () =>
            {
                Assert.That(dismounted, Is.True);
                Assert.That(mobileService.LastRiderId, Is.EqualTo((Serial)0x200));
                Assert.That(mobileService.DismountCalls, Is.EqualTo(1));
            }
        );
    }

    [Test]
    public void TryMount_ShouldPersistRuntimeRiderAndMountBeforeDelegating()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var sessionService = new FakeGameNetworkSessionService();
        var spatialService = new RegionDataLoaderTestSpatialWorldService();
        var mobileService = new MobileModuleTestMobileService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var rider = new UOMobileEntity
        {
            Id = (Serial)0x200,
            Name = "Rider",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        var mount = new UOMobileEntity
        {
            Id = (Serial)0x300,
            Name = "Horse",
            MapId = 1,
            Location = new(100, 100, 0)
        };
        mobileService.Register(rider);
        mobileService.Register(mount);
        spatialService.AddOrUpdateMobile(mount);
        var session = new GameSession(new(client))
        {
            CharacterId = rider.Id,
            Character = rider
        };
        sessionService.Add(session);
        var module = new MobileCombatModule(
            mobileService,
            sessionService,
            spatialService,
            outgoing,
            null
        );

        var mounted = module.TryMount(rider.Id, mount.Id, rider, mount);

        Assert.Multiple(
            () =>
            {
                Assert.That(mounted, Is.True);
                Assert.That(mobileService.CreateOrUpdateCalls, Is.EqualTo([(Serial)0x200, (Serial)0x300]));
                Assert.That(mobileService.LastRiderId, Is.EqualTo((Serial)0x200));
                Assert.That(mobileService.LastMountId, Is.EqualTo((Serial)0x300));
            }
        );
    }
}
