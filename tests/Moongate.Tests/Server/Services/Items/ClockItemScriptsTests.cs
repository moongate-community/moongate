using System.Net.Sockets;
using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Scripting.Services;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Modules;
using Moongate.Server.Services.Items;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Services.Items;

public sealed class ClockItemScriptsTests
{
    private sealed class ClockItemScriptsTestItemService : IItemService
    {
        public Task BulkUpsertItemsAsync(IReadOnlyList<UOItemEntity> items)
            => throw new NotSupportedException();

        public UOItemEntity Clone(UOItemEntity item, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> CloneAsync(Serial itemId, bool generateNewSerial = true)
            => throw new NotSupportedException();

        public Task<Serial> CreateItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task<bool> DeleteItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<DropItemToGroundResult?> DropItemToGroundAsync(
            Serial itemId,
            Point3D location,
            int mapId,
            long sessionId = 0
        )
            => throw new NotSupportedException();

        public Task<bool> EquipItemAsync(Serial itemId, Serial mobileId, ItemLayerType layer)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetGroundItemsInSectorAsync(int mapId, int sectorX, int sectorY)
            => throw new NotSupportedException();

        public Task<UOItemEntity?> GetItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task<List<UOItemEntity>> GetItemsInContainerAsync(Serial containerId)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToContainerAsync(Serial itemId, Serial containerId, Point2D position, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<bool> MoveItemToWorldAsync(Serial itemId, Point3D location, int mapId, long sessionId = 0)
            => throw new NotSupportedException();

        public Task<UOItemEntity> SpawnFromTemplateAsync(string itemTemplateId)
            => throw new NotSupportedException();

        public Task<(bool Found, UOItemEntity? Item)> TryToGetItemAsync(Serial itemId)
            => throw new NotSupportedException();

        public Task UpsertItemAsync(UOItemEntity item)
            => throw new NotSupportedException();

        public Task UpsertItemsAsync(params UOItemEntity[] items)
            => throw new NotSupportedException();
    }

    private sealed class ClockItemScriptsTestSpeechService : ISpeechService
    {
        public List<(long SessionId, string Text)> SentMessages { get; } = [];

        public Task<int> BroadcastFromServerAsync(string text, short hue = 946, short font = 3, string language = "ENU")
            => Task.FromResult(0);

        public Task HandleOpenChatWindowAsync(
            GameSession session,
            OpenChatWindowPacket packet,
            CancellationToken cancellationToken = default
        )
            => Task.CompletedTask;

        public Task<UnicodeSpeechMessagePacket?> ProcessIncomingSpeechAsync(
            GameSession session,
            UnicodeSpeechPacket speechPacket,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult<UnicodeSpeechMessagePacket?>(null);

        public Task<bool> SendMessageFromServerAsync(
            GameSession session,
            string text,
            short hue = 946,
            short font = 3,
            string language = "ENU"
        )
        {
            SentMessages.Add((session.SessionId, text));

            return Task.FromResult(true);
        }

        public Task<int> SpeakAsMobileAsync(
            UOMobileEntity speaker,
            string text,
            int range = 12,
            ChatMessageType messageType = ChatMessageType.Regular,
            short hue = SpeechHues.Default,
            short font = SpeechHues.DefaultFont,
            string language = "ENU",
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(0);
    }

    [Test]
    public async Task DispatchAsync_WhenClockIsUsed_ShouldSendTimeMessagesToPlayer()
    {
        using var temp = new TempDirectory();
        var directories = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDirectory = directories[DirectoryType.Scripts];
        Directory.CreateDirectory(scriptsDirectory);
        Directory.CreateDirectory(Path.Combine(scriptsDirectory, "items"));

        await File.WriteAllTextAsync(Path.Combine(scriptsDirectory, "init.lua"), "require(\"items.clock\")");
        File.Copy(
            Path.GetFullPath(
                Path.Combine(
                    TestContext.CurrentContext.TestDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "moongate_data",
                    "scripts",
                    "items",
                    "clock.lua"
                )
            ),
            Path.Combine(scriptsDirectory, "items", "clock.lua"),
            true
        );

        var sessionService = new FakeGameNetworkSessionService();
        var speechService = new ClockItemScriptsTestSpeechService();
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var mobile = new UOMobileEntity
        {
            Id = (Serial)0x330,
            MapId = 0,
            Location = Point3D.Zero
        };
        var session = new GameSession(new(client))
        {
            CharacterId = mobile.Id,
            Character = mobile
        };
        sessionService.Add(session);

        var container = new Container();
        container.RegisterInstance<IGameNetworkSessionService>(sessionService);
        container.RegisterInstance<ISpeechService>(speechService);
        container.RegisterInstance<IItemService>(new ClockItemScriptsTestItemService());
        container.RegisterInstance(
            new MoongateSpatialConfig
            {
                LightWorldStartUtc = "1997-09-01T00:00:00Z",
                LightSecondsPerUoMinute = 5
            }
        );

        using var scriptEngine = new LuaScriptEngineService(
            directories,
            [new(typeof(SpeechModule)), new(typeof(ClockModule))],
            container,
            new(temp.Path, scriptsDirectory, "0.1.0"),
            []
        );
        await scriptEngine.StartAsync();

        var dispatcher = new ItemScriptDispatcher(
            scriptEngine,
            new ClockItemScriptsTestItemService(),
            sessionService
        );

        var dispatched = await dispatcher.DispatchAsync(
                             new(
                                 session,
                                 new()
                                 {
                                     Id = (Serial)0x440,
                                     Name = "Clock",
                                     ScriptId = "items.clock",
                                     ItemId = 0x104B
                                 },
                                 "double_click"
                             )
                         );

        Assert.Multiple(
            () =>
            {
                Assert.That(dispatched, Is.True);
                Assert.That(speechService.SentMessages, Has.Count.EqualTo(2));
                Assert.That(speechService.SentMessages[0].SessionId, Is.EqualTo(session.SessionId));
                Assert.That(speechService.SentMessages[0].Text, Is.Not.Empty);
                Assert.That(speechService.SentMessages[1].Text, Does.EndWith("to be exact."));
            }
        );
    }
}
