using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Spans;
using Moongate.Server.Services.Entities;
using Moongate.Server.Services.Persistence;
using Moongate.Server.Services.Timing;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Names;
using Moongate.UO.Data.Services.Templates;
using Moongate.UO.Data.Templates.Items;
using Moongate.UO.Data.Templates.Mobiles;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Entities;

public class MobileFactoryServiceTests
{
    private sealed class TestNameService : INameService
    {
        public string NextGeneratedName { get; set; }

        public void AddNames(string type, params string[] names)
        {
            _ = type;
            _ = names;
        }

        public string GenerateName(string type)
        {
            _ = type;

            return NextGeneratedName;
        }

        public string GenerateName(MobileTemplateDefinition mobileTemplate)
        {
            _ = mobileTemplate;

            return NextGeneratedName;
        }
    }

    [Test]
    public async Task CreateMobileFromTemplate_ShouldApplyTypedParamsToCustomProperties()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new MobileTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "param_mobile",
                Name = "Param Mobile",
                Category = "test",
                Description = "test",
                Body = 0x0190,
                SkinHue = HueSpec.FromValue(0),
                HairHue = HueSpec.FromValue(0),
                HairStyle = 0,
                Params = new()
                {
                    ["title_suffix"] = new() { Type = ItemTemplateParamType.String, Value = "the brave" },
                    ["owner_id"] = new() { Type = ItemTemplateParamType.Serial, Value = "0x00001234" },
                    ["marker_hue"] = new() { Type = ItemTemplateParamType.Hue, Value = "0x044D" }
                }
            }
        );
        var nameService = new TestNameService();
        var service = new MobileFactoryService(templateService, nameService, persistence);

        var mobile = service.CreateMobileFromTemplate("param_mobile");

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.TryGetCustomString("title_suffix", out var titleSuffix), Is.True);
                Assert.That(titleSuffix, Is.EqualTo("the brave"));
                Assert.That(mobile.TryGetCustomInteger("owner_id", out var ownerId), Is.True);
                Assert.That(ownerId, Is.EqualTo(0x00001234));
                Assert.That(mobile.TryGetCustomInteger("marker_hue", out var markerHue), Is.True);
                Assert.That(markerHue, Is.EqualTo(0x044D));
            }
        );
    }

    [Test]
    public async Task CreateMobileFromTemplate_ShouldClampHits_WhenMaxHitsIsLower()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new MobileTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "boss",
                Name = "Boss",
                Category = "boss",
                Description = "boss",
                Body = 0x0011,
                SkinHue = HueSpec.FromValue(0),
                HairHue = HueSpec.FromValue(0),
                HairStyle = 0,
                Hits = 120,
                MaxHits = 80
            }
        );
        var nameService = new TestNameService();
        var service = new MobileFactoryService(templateService, nameService, persistence);

        var mobile = service.CreateMobileFromTemplate("boss");

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.MaxHits, Is.EqualTo(80));
                Assert.That(mobile.Hits, Is.EqualTo(50));
            }
        );
    }

    [Test]
    public async Task CreateMobileFromTemplate_ShouldMapTemplateFieldsAndAllocateSerial()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new MobileTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "orc",
                Name = "Orc Warrior",
                Category = "orc",
                Description = "orc",
                Body = 0x0011,
                SkinHue = HueSpec.FromValue(1000),
                HairHue = HueSpec.FromValue(1109),
                HairStyle = 8251,
                Strength = 70,
                Dexterity = 60,
                Intelligence = 20,
                Hits = 70,
                Mana = 20,
                Stamina = 60,
                Notoriety = Notoriety.Criminal
            }
        );
        var nameService = new TestNameService
        {
            NextGeneratedName = "Gor"
        };
        var service = new MobileFactoryService(templateService, nameService, persistence);

        var mobile = service.CreateMobileFromTemplate("orc", (Serial)25);

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.Id.IsMobile, Is.True);
                Assert.That(mobile.AccountId, Is.EqualTo((Serial)25));
                Assert.That(mobile.Name, Is.EqualTo("Orc Warrior"));
                Assert.That((int)mobile.Body, Is.EqualTo(0x0011));
                Assert.That(mobile.SkinHue, Is.EqualTo(1000));
                Assert.That(mobile.HairHue, Is.EqualTo(1109));
                Assert.That(mobile.HairStyle, Is.EqualTo(8251));
                Assert.That(mobile.Strength, Is.EqualTo(70));
                Assert.That(mobile.Dexterity, Is.EqualTo(60));
                Assert.That(mobile.Intelligence, Is.EqualTo(20));
                Assert.That(mobile.Hits, Is.EqualTo(70));
                Assert.That(mobile.Mana, Is.EqualTo(20));
                Assert.That(mobile.Stamina, Is.EqualTo(60));
                Assert.That(mobile.Notoriety, Is.EqualTo(Notoriety.Criminal));
                Assert.That(mobile.IsPlayer, Is.False);
                Assert.That(mobile.Location, Is.EqualTo(Point3D.Zero));
            }
        );
    }

    [Test]
    public async Task CreateMobileFromTemplate_ShouldThrow_WhenSerialParamIsInvalid()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new MobileTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "invalid_serial_param_mobile",
                Name = "Invalid Serial Param Mobile",
                Category = "test",
                Description = "test",
                Body = 0x0190,
                SkinHue = HueSpec.FromValue(0),
                HairHue = HueSpec.FromValue(0),
                HairStyle = 0,
                Params = new()
                {
                    ["owner_id"] = new() { Type = ItemTemplateParamType.Serial, Value = "bad-serial" }
                }
            }
        );
        var nameService = new TestNameService();
        var service = new MobileFactoryService(templateService, nameService, persistence);

        Assert.That(
            () => service.CreateMobileFromTemplate("invalid_serial_param_mobile"),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("invalid serial param")
        );
    }

    [TestCase(null), TestCase(""), TestCase(" ")]
    public async Task CreateMobileFromTemplate_ShouldThrow_WhenTemplateIdIsInvalid(string? templateId)
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new MobileTemplateService();
        var nameService = new TestNameService();
        var service = new MobileFactoryService(templateService, nameService, persistence);

        Assert.That(
            () => service.CreateMobileFromTemplate(templateId!),
            Throws.InstanceOf<ArgumentException>()
        );
    }

    [Test]
    public async Task CreateMobileFromTemplate_ShouldThrow_WhenTemplateIsMissing()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new MobileTemplateService();
        var nameService = new TestNameService();
        var service = new MobileFactoryService(templateService, nameService, persistence);

        Assert.That(
            () => service.CreateMobileFromTemplate("missing_template"),
            Throws.TypeOf<InvalidOperationException>()
        );
    }

    [Test]
    public async Task CreateMobileFromTemplate_ShouldUseGeneratedName_WhenTemplateNameIsEmpty()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new MobileTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "nameless_orc",
                Name = string.Empty,
                Title = string.Empty,
                Category = "orc",
                Description = "orc",
                Body = 0x0011,
                SkinHue = HueSpec.FromValue(1000),
                HairHue = HueSpec.FromValue(1109),
                HairStyle = 8251
            }
        );
        var nameService = new TestNameService
        {
            NextGeneratedName = "Gor"
        };
        var service = new MobileFactoryService(templateService, nameService, persistence);

        var mobile = service.CreateMobileFromTemplate("nameless_orc");

        Assert.That(mobile.Name, Is.EqualTo("Gor"));
    }

    [Test]
    public async Task CreateMobileFromTemplate_ShouldUseTemplateName_WhenGeneratedNameIsEmpty()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new MobileTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "human_vendor",
                Name = "Human Vendor",
                Category = "human",
                Description = "vendor",
                Body = 0x0190,
                SkinHue = HueSpec.FromValue(1000),
                HairHue = HueSpec.FromValue(1109),
                HairStyle = 8251
            }
        );
        var nameService = new TestNameService
        {
            NextGeneratedName = string.Empty
        };
        var service = new MobileFactoryService(templateService, nameService, persistence);

        var mobile = service.CreateMobileFromTemplate("human_vendor");

        Assert.That(mobile.Name, Is.EqualTo("Human Vendor"));
    }

    [Test]
    public async Task CreateMobileFromTemplate_ShouldUseZeroAccount_WhenNotProvided()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new MobileTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "wanderer",
                Name = "Wanderer",
                Category = "human",
                Description = "wanderer",
                Body = 0x0190,
                SkinHue = HueSpec.FromValue(0),
                HairHue = HueSpec.FromValue(0),
                HairStyle = 0
            }
        );
        var nameService = new TestNameService();
        var service = new MobileFactoryService(templateService, nameService, persistence);

        var mobile = service.CreateMobileFromTemplate("wanderer");

        Assert.That(mobile.AccountId, Is.EqualTo(Serial.Zero));
    }

    [Test]
    public async Task CreateMobileFromTemplate_WhenSellProfileIsConfigured_ShouldBindSellProfileCustomProperty()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new MobileTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "vendor_with_profile",
                Name = "Vendor",
                Category = "human",
                Description = "vendor",
                Body = 0x0190,
                SkinHue = HueSpec.FromValue(0),
                HairHue = HueSpec.FromValue(0),
                HairStyle = 0,
                SellProfileId = "vendor.blacksmith"
            }
        );

        var sellProfileService = new SellProfileTemplateService();
        sellProfileService.Upsert(
            new()
            {
                Id = "vendor.blacksmith",
                Name = "Blacksmith Vendor",
                Category = "vendors",
                Description = "blacksmith"
            }
        );

        var service = new MobileFactoryService(
            templateService,
            new TestNameService(),
            persistence,
            sellProfileService
        );

        var mobile = service.CreateMobileFromTemplate("vendor_with_profile");

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.TryGetCustomString("sell_profile_id", out var profileId), Is.True);
                Assert.That(profileId, Is.EqualTo("vendor.blacksmith"));
            }
        );
    }

    [Test]
    public async Task CreateMobileFromTemplate_WhenSellProfileIsMissing_ShouldThrow()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new MobileTemplateService();
        templateService.Upsert(
            new()
            {
                Id = "vendor_with_profile",
                Name = "Vendor",
                Category = "human",
                Description = "vendor",
                Body = 0x0190,
                SkinHue = HueSpec.FromValue(0),
                HairHue = HueSpec.FromValue(0),
                HairStyle = 0,
                SellProfileId = "vendor.missing"
            }
        );

        var sellProfileService = new SellProfileTemplateService();
        var service = new MobileFactoryService(
            templateService,
            new TestNameService(),
            persistence,
            sellProfileService
        );

        Assert.That(
            () => service.CreateMobileFromTemplate("vendor_with_profile"),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("missing sell profile")
        );
    }

    [Test]
    public async Task CreatePlayerMobile_ShouldMapPacketFieldsAndAllocateSerial()
    {
        using var temp = new TempDirectory();
        var persistence = await CreatePersistenceServiceAsync(temp.Path);
        var templateService = new MobileTemplateService();
        var nameService = new TestNameService();
        var service = new MobileFactoryService(templateService, nameService, persistence);
        var packet = new CharacterCreationPacket();
        _ = packet.TryParse(BuildCharacterCreationPayload());
        var expectedLocation = packet.StartingCity?.Location ?? Point3D.Zero;
        var expectedMapId = packet.StartingCity?.Map?.Index ?? 0;

        var mobile = service.CreatePlayerMobile(packet, (Serial)0x00000101);

        Assert.Multiple(
            () =>
            {
                Assert.That(mobile.Id.IsMobile, Is.True);
                Assert.That(mobile.AccountId, Is.EqualTo((Serial)0x00000101));
                Assert.That(mobile.Name, Is.EqualTo("TestCharacter"));
                Assert.That(mobile.Location, Is.EqualTo(expectedLocation));
                Assert.That(mobile.MapId, Is.EqualTo(expectedMapId));
                Assert.That(mobile.Gender, Is.EqualTo(GenderType.Female));
                Assert.That(mobile.ProfessionId, Is.EqualTo(2));
                Assert.That(mobile.IsPlayer, Is.True);
                Assert.That(mobile.IsAlive, Is.True);
                Assert.That(mobile.Notoriety, Is.EqualTo(Notoriety.Innocent));
            }
        );
    }

    private static byte[] BuildCharacterCreationPayload()
    {
        var writer = new SpanWriter(106, true);

        writer.Write((byte)0xF8);
        writer.Write(unchecked((int)0xEDEDEDED));
        writer.Write(unchecked((int)0xFFFFFFFF));
        writer.Write((byte)0x00);
        writer.WriteAscii("TestCharacter", 30);
        writer.Write((ushort)0);
        writer.Write((uint)ClientFlags.Trammel);
        writer.Write(0);
        writer.Write(0);
        writer.Write((byte)2);
        writer.Clear(15);
        writer.Write((byte)5);
        writer.Write((byte)60);
        writer.Write((byte)50);
        writer.Write((byte)40);
        writer.Write((byte)UOSkillName.Magery);
        writer.Write((byte)50);
        writer.Write((byte)UOSkillName.Meditation);
        writer.Write((byte)50);
        writer.Write((byte)UOSkillName.EvalInt);
        writer.Write((byte)50);
        writer.Write((byte)UOSkillName.Wrestling);
        writer.Write((byte)50);
        writer.Write((short)0x0455);
        writer.Write((short)0x0203);
        writer.Write((short)0x0304);
        writer.Write((short)0x0506);
        writer.Write((short)0x0708);
        writer.Write((short)0);
        writer.Write((ushort)0);
        writer.Write((short)1);
        writer.Write(0);
        writer.Write((short)0x0888);
        writer.Write((short)0x0999);

        var result = writer.ToArray();
        writer.Dispose();

        return result;
    }

    private static async Task<PersistenceService> CreatePersistenceServiceAsync(string rootDirectory)
    {
        var directories = new DirectoriesConfig(rootDirectory, Enum.GetNames<DirectoryType>());
        var persistence = new PersistenceService(
            directories,
            new TimerWheelService(
                new()
                {
                    TickDuration = TimeSpan.FromMilliseconds(250),
                    WheelSize = 512
                }
            ),
            new(),
            new NetworkServiceTestGameEventBusService()
        );

        await persistence.StartAsync();

        return persistence;
    }
}
