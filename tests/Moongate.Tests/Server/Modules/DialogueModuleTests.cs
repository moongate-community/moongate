using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Modules;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Modules;

public sealed class DialogueModuleTests
{
    private sealed class DialogueModuleSpeechServiceStub : ISpeechService
    {
        public List<(Serial SpeakerId, string Text, ChatMessageType MessageType)> Calls { get; } = [];

        public Task<int> BroadcastFromServerAsync(
            string text,
            short hue = SpeechHues.System,
            short font = SpeechHues.DefaultFont,
            string language = "ENU"
        )
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
            short hue = SpeechHues.System,
            short font = SpeechHues.DefaultFont,
            string language = "ENU"
        )
            => Task.FromResult(true);

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
        {
            Calls.Add((speaker.Id, text, messageType));
            return Task.FromResult(1);
        }
    }

    private sealed class DialogueModuleSessionServiceStub : IGameNetworkSessionService
    {
        public int Count => 0;
        public void Clear() { }
        public IReadOnlyCollection<GameSession> GetAll() => [];
        public GameSession GetOrCreate(MoongateTCPClient client) => throw new NotImplementedException();
        public bool Remove(long sessionId) => false;
        public bool TryGet(long sessionId, out GameSession session) { session = null!; return false; }
        public bool TryGetByCharacterId(Serial characterId, out GameSession session) { session = null!; return false; }
    }

    [Test]
    public void Init_ShouldBindConversationIdOnNpc()
    {
        var module = CreateModule(Path.GetTempPath(), out _, out _);
        var npc = CreateProxy((Serial)0x100u, "Innkeeper");

        var result = module.Init(npc, "innkeeper");

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(npc.Mobile.TryGetCustomString("dialogue_id", out var conversationId), Is.True);
                Assert.That(conversationId, Is.EqualTo("innkeeper"));
            }
        );
    }

    [Test]
    public void Listener_WhenTopicMatches_ShouldStartDialogueAndSpeakNodeAndOptions()
    {
        using var tempDirectory = new TempDirectory();
        var module = CreateModule(tempDirectory.Path, out var definitions, out var speech);
        RegisterConversation(
            definitions,
            """
            return {
                start = "start",
                topics = {
                    room = { "room", "stanza" }
                },
                topic_routes = {
                    room = "room_offer"
                },
                nodes = {
                    start = { text = "Start", options = {} },
                    room_offer = {
                        text = "A room costs 15 gold.",
                        options = {
                            { text = "Accept", goto_ = "done" },
                            { text = "No thanks", goto_ = "done" }
                        }
                    },
                    done = { text = "Done", options = {} }
                }
            }
            """
        );

        var npc = CreateProxy((Serial)0x100u, "Innkeeper");
        var speaker = CreateProxy((Serial)0x200u, "Player");
        _ = module.Init(npc, "innkeeper");

        var handled = module.Listener(npc, speaker, "vorrei una stanza");

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(speech.Calls, Has.Count.EqualTo(2));
                Assert.That(speech.Calls[0].Text, Is.EqualTo("A room costs 15 gold."));
                Assert.That(speech.Calls[1].Text, Is.EqualTo("1. Accept 2. No thanks"));
            }
        );
    }

    [Test]
    public void Listener_WhenSessionIsActiveAndNumericChoice_ShouldAdvanceDialogue()
    {
        using var tempDirectory = new TempDirectory();
        var module = CreateModule(tempDirectory.Path, out var definitions, out var speech);
        RegisterConversation(
            definitions,
            """
            return {
                start = "start",
                topics = {
                    room = { "room" }
                },
                topic_routes = {
                    room = "room_offer"
                },
                nodes = {
                    start = { text = "Start", options = {} },
                    room_offer = {
                        text = "A room costs 15 gold.",
                        options = {
                            { text = "Accept", goto_ = "done" }
                        }
                    },
                    done = { text = "Your room is upstairs.", options = {} }
                }
            }
            """
        );

        var npc = CreateProxy((Serial)0x100u, "Innkeeper");
        var speaker = CreateProxy((Serial)0x200u, "Player");
        _ = module.Init(npc, "innkeeper");
        _ = module.Listener(npc, speaker, "room");
        speech.Calls.Clear();

        var handled = module.Listener(npc, speaker, "1");

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(speech.Calls, Has.Count.EqualTo(1));
                Assert.That(speech.Calls[0].Text, Is.EqualTo("Your room is upstairs."));
            }
        );
    }

    private static void RegisterConversation(IDialogueDefinitionService definitions, string body)
    {
        var script = new Script();
        var definition = script.DoString(body).Table!;
        _ = definitions.Register("innkeeper", definition, "scripts/dialogues/innkeeper.lua");
    }

    private static DialogueModule CreateModule(
        string root,
        out IDialogueDefinitionService definitions,
        out DialogueModuleSpeechServiceStub speechService
    )
    {
        definitions = new DialogueDefinitionService();
        speechService = new DialogueModuleSpeechServiceStub();
        var runtime = new DialogueRuntimeService(
            definitions,
            new DialogueMemoryService(new Moongate.Core.Data.Directories.DirectoriesConfig(root, Enum.GetNames<Moongate.Core.Types.DirectoryType>())),
            speechService,
            new DialogueModuleSessionServiceStub()
        );

        return new DialogueModule(definitions, runtime, speechService);
    }

    private static LuaMobileProxy CreateProxy(Serial serial, string name)
    {
        var mobile = new UOMobileEntity
        {
            Id = serial,
            Name = name,
            IsAlive = true,
            MapId = 1,
            Location = new Point3D(100, 100, 0)
        };

        return new LuaMobileProxy(mobile, new DialogueModuleSpeechServiceStub(), new DialogueModuleSessionServiceStub());
    }
}
