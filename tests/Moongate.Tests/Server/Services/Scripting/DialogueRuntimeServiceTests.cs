using Moongate.Core.Types;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class DialogueRuntimeServiceTests
{
    private sealed class DialogueRuntimeSpeechServiceStub : ISpeechService
    {
        public List<(Serial SpeakerId, string Text, ChatMessageType MessageType)> Calls { get; } = [];

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
            => Task.FromResult(true);

        public Task<int> SpeakAsMobileAsync(
            UOMobileEntity speaker,
            string text,
            int range = 12,
            ChatMessageType messageType = ChatMessageType.Regular,
            short hue = 0x03B2,
            short font = 3,
            string language = "ENU",
            CancellationToken cancellationToken = default
        )
        {
            Calls.Add((speaker.Id, text, messageType));

            return Task.FromResult(1);
        }
    }

    private sealed class DialogueRuntimeSessionServiceStub : IGameNetworkSessionService
    {
        public int Count => 0;

        public void Clear() { }

        public IReadOnlyCollection<GameSession> GetAll()
            => [];

        public GameSession GetOrCreate(MoongateTCPClient client)
            => throw new NotImplementedException();

        public bool Remove(long sessionId)
            => false;

        public bool TryGet(long sessionId, out GameSession gameSession)
        {
            gameSession = null!;

            return false;
        }

        public bool TryGetByCharacterId(Serial characterId, out GameSession gameSession)
        {
            gameSession = null!;

            return false;
        }
    }

    [Test]
    public async Task ChooseAsync_ShouldApplyEffects_AdvanceNode_AndPersistMemory()
    {
        using var tempDirectory = new TempDirectory();
        var runtime = CreateRuntime(tempDirectory.Path, out var definitions, out _);
        RegisterConversation(
            definitions,
            BuildConversation(
                new(),
                """
                return {
                    start = "start",
                    nodes = {
                        start = {
                            text = "Start",
                            options = {
                                {
                                    text = "Accept",
                                    effects = function(ctx)
                                        ctx:set_memory_flag("accepted", true)
                                        ctx:add_memory_number("rooms_rented", 1)
                                        ctx:set_flag("visited", true)
                                    end,
                                    goto_ = "done"
                                }
                            }
                        },
                        done = { text = "Done", options = {} }
                    }
                }
                """
            )
        );

        var npc = CreateNpc();
        var listener = CreateListener();

        _ = await runtime.StartAsync(npc, listener, "innkeeper");
        var session = await runtime.ChooseAsync(npc, listener, 1);

        var memoryService = new DialogueMemoryService(new(tempDirectory.Path, Enum.GetNames<DirectoryType>()));
        var entry = memoryService.GetOrCreateEntry(npc.Id, listener.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(session, Is.Not.Null);
                Assert.That(session!.CurrentNodeId, Is.EqualTo("done"));
                Assert.That(session.SessionFlags["visited"], Is.True);
                Assert.That(entry.Flags["accepted"], Is.True);
                Assert.That(entry.Numbers["rooms_rented"], Is.EqualTo(1));
                Assert.That(entry.LastNode, Is.EqualTo("done"));
            }
        );
    }

    [Test]
    public async Task ChooseAsync_WhenEffectEndsConversation_ShouldRemoveSession()
    {
        using var tempDirectory = new TempDirectory();
        var runtime = CreateRuntime(tempDirectory.Path, out var definitions, out var speech);
        RegisterConversation(
            definitions,
            BuildConversation(
                new(),
                """
                return {
                    start = "start",
                    nodes = {
                        start = {
                            text = "Start",
                            options = {
                                {
                                    text = "Bye",
                                    effects = function(ctx)
                                        ctx:emote("*waves*")
                                        ctx:end_conversation()
                                    end,
                                    goto_ = "done"
                                }
                            }
                        },
                        done = { text = "Done", options = {} }
                    }
                }
                """
            )
        );

        var npc = CreateNpc();
        var listener = CreateListener();

        _ = await runtime.StartAsync(npc, listener, "innkeeper");
        var result = await runtime.ChooseAsync(npc, listener, 1);
        var stillActive = runtime.TryGetActiveSession(npc.Id, listener.Id, out _);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.Null);
                Assert.That(stillActive, Is.False);
                Assert.That(speech.Calls, Has.Count.EqualTo(1));
                Assert.That(speech.Calls[0].MessageType, Is.EqualTo(ChatMessageType.Emote));
            }
        );
    }

    [Test]
    public async Task HandleTopicAsync_ShouldMatchAlias_AndJumpToTopicRoute()
    {
        using var tempDirectory = new TempDirectory();
        var runtime = CreateRuntime(tempDirectory.Path, out var definitions, out _);
        RegisterConversation(
            definitions,
            BuildConversation(
                new(),
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
                        room_offer = { text = "A room costs 15 gold.", options = {} }
                    }
                }
                """
            )
        );

        var npc = CreateNpc();
        var listener = CreateListener();
        var session = await runtime.HandleTopicAsync(npc, listener, "innkeeper", "Hai una stanza libera?");

        Assert.Multiple(
            () =>
            {
                Assert.That(session, Is.Not.Null);
                Assert.That(session!.CurrentNodeId, Is.EqualTo("room_offer"));
                Assert.That(session.LastTopicId, Is.EqualTo("room"));
            }
        );
    }

    [Test]
    public async Task StartAsync_ShouldCreateSession_AndFilterOptionsByCondition()
    {
        using var tempDirectory = new TempDirectory();
        var runtime = CreateRuntime(tempDirectory.Path, out var definitions, out _);
        var script = new Script();
        RegisterConversation(
            definitions,
            BuildConversation(
                script,
                """
                return {
                    start = "start",
                    nodes = {
                        start = {
                            text = "Welcome",
                            options = {
                                { text = "Visible", goto_ = "bye" },
                                {
                                    text = "Hidden",
                                    condition = function(ctx)
                                        return ctx:get_memory_flag("show_hidden")
                                    end,
                                    goto_ = "bye"
                                }
                            }
                        },
                        bye = { text = "Bye", options = {} }
                    }
                }
                """
            )
        );

        var session = await runtime.StartAsync(CreateNpc(), CreateListener(), "innkeeper");

        Assert.Multiple(
            () =>
            {
                Assert.That(session, Is.Not.Null);
                Assert.That(session!.CurrentNodeId, Is.EqualTo("start"));
                Assert.That(session.VisibleOptions.Select(static option => option.Text), Is.EqualTo(new[] { "Visible" }));
            }
        );
    }

    private static Table BuildConversation(Script script, string body)
        => script.DoString(body).Table!;

    private static UOMobileEntity CreateListener()
        => new()
        {
            Id = (Serial)0x200u,
            Name = "Player",
            IsAlive = true,
            IsPlayer = true,
            MapId = 1,
            Location = new(100, 101, 0)
        };

    private static UOMobileEntity CreateNpc()
        => new()
        {
            Id = (Serial)0x100u,
            Name = "Innkeeper",
            IsAlive = true,
            MapId = 1,
            Location = new(100, 100, 0)
        };

    private static DialogueRuntimeService CreateRuntime(
        string root,
        out IDialogueDefinitionService definitions,
        out DialogueRuntimeSpeechServiceStub speechService
    )
    {
        definitions = new DialogueDefinitionService();
        var memoryService = new DialogueMemoryService(new(root, Enum.GetNames<DirectoryType>()));
        speechService = new();

        return new(
            definitions,
            memoryService,
            speechService,
            new DialogueRuntimeSessionServiceStub()
        );
    }

    private static void RegisterConversation(IDialogueDefinitionService definitions, Table definition)
        => definitions.Register("innkeeper", definition, "scripts/dialogues/innkeeper.lua");
}
