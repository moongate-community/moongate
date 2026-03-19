using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.Speech;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.Server.Modules;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Modules;

public sealed class AiDialogueModuleTests
{
    private sealed class AiDialogueModuleTestSpeechService : ISpeechService
    {
        public Task<int> BroadcastFromServerAsync(
            string text,
            short hue = SpeechHues.System,
            short font = SpeechHues.DefaultFont,
            string language = "ENU"
        )
        {
            _ = text;
            _ = hue;
            _ = font;
            _ = language;

            return Task.FromResult(0);
        }

        public Task HandleOpenChatWindowAsync(
            GameSession session,
            OpenChatWindowPacket packet,
            CancellationToken cancellationToken = default
        )
        {
            _ = session;
            _ = packet;
            _ = cancellationToken;

            return Task.CompletedTask;
        }

        public Task<UnicodeSpeechMessagePacket?> ProcessIncomingSpeechAsync(
            GameSession session,
            UnicodeSpeechPacket speechPacket,
            CancellationToken cancellationToken = default
        )
        {
            _ = session;
            _ = speechPacket;
            _ = cancellationToken;

            return Task.FromResult<UnicodeSpeechMessagePacket?>(null);
        }

        public Task<bool> SendMessageFromServerAsync(
            GameSession session,
            string text,
            short hue = SpeechHues.System,
            short font = SpeechHues.DefaultFont,
            string language = "ENU"
        )
        {
            _ = session;
            _ = text;
            _ = hue;
            _ = font;
            _ = language;

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
        {
            _ = speaker;
            _ = text;
            _ = range;
            _ = messageType;
            _ = hue;
            _ = font;
            _ = language;
            _ = cancellationToken;

            return Task.FromResult(0);
        }
    }

    private sealed class AiDialogueModuleTestGameNetworkSessionService : IGameNetworkSessionService
    {
        public int Count => 0;

        public void Clear() { }

        public IReadOnlyCollection<GameSession> GetAll()
            => [];

        public GameSession GetOrCreate(MoongateTCPClient client)
            => throw new NotImplementedException();

        public bool Remove(long sessionId)
        {
            _ = sessionId;

            return false;
        }

        public bool TryGet(long sessionId, out GameSession session)
        {
            _ = sessionId;
            session = null!;

            return false;
        }

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            _ = characterId;
            session = null!;

            return false;
        }
    }

    private sealed class AiDialogueModuleTestRuntimeStateService : INpcAiRuntimeStateService
    {
        public Serial BoundNpcId { get; private set; }

        public string? BoundPromptFile { get; private set; }

        public void BindPromptFile(Serial npcId, string promptFile)
        {
            BoundNpcId = npcId;
            BoundPromptFile = promptFile;
        }

        public bool TryAcquireIdle(Serial npcId, long nowMilliseconds, int cooldownMilliseconds)
        {
            _ = npcId;
            _ = nowMilliseconds;
            _ = cooldownMilliseconds;

            return true;
        }

        public bool TryAcquireListener(Serial npcId, long nowMilliseconds, int cooldownMilliseconds)
        {
            _ = npcId;
            _ = nowMilliseconds;
            _ = cooldownMilliseconds;

            return true;
        }

        public bool TryGetPromptFile(Serial npcId, out string? promptFile)
        {
            promptFile = BoundNpcId == npcId ? BoundPromptFile : null;

            return promptFile is not null;
        }
    }

    private sealed class AiDialogueModuleTestDialogueService : INpcDialogueService
    {
        public UOMobileEntity? LastIdleNpc { get; private set; }

        public UOMobileEntity? LastListenerNpc { get; private set; }

        public UOMobileEntity? LastSender { get; private set; }

        public string? LastText { get; private set; }

        public bool QueueIdle(UOMobileEntity npc)
        {
            LastIdleNpc = npc;

            return true;
        }

        public bool QueueListener(UOMobileEntity npc, UOMobileEntity sender, string text)
        {
            LastListenerNpc = npc;
            LastSender = sender;
            LastText = text;

            return true;
        }
    }

    [Test]
    public void Idle_ShouldQueueDialogueWork()
    {
        var runtimeState = new AiDialogueModuleTestRuntimeStateService();
        var dialogueService = new AiDialogueModuleTestDialogueService();
        var module = new AiDialogueModule(runtimeState, dialogueService);
        var npc = CreateProxy((Serial)0x100u, "Lilly");

        var handled = module.Idle(npc);

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(dialogueService.LastIdleNpc, Is.Not.Null);
                Assert.That(dialogueService.LastIdleNpc!.Id, Is.EqualTo((Serial)0x100u));
            }
        );
    }

    [Test]
    public void Init_ShouldBindPromptFileForNpc()
    {
        var runtimeState = new AiDialogueModuleTestRuntimeStateService();
        var dialogueService = new AiDialogueModuleTestDialogueService();
        var module = new AiDialogueModule(runtimeState, dialogueService);
        var npc = CreateProxy((Serial)0x100u, "Lilly");

        var initialized = module.Init(npc, "lilly.txt");

        Assert.Multiple(
            () =>
            {
                Assert.That(initialized, Is.True);
                Assert.That(runtimeState.BoundNpcId, Is.EqualTo((Serial)0x100u));
                Assert.That(runtimeState.BoundPromptFile, Is.EqualTo("lilly.txt"));
            }
        );
    }

    [Test]
    public void Listener_ShouldQueueDialogueWork()
    {
        var runtimeState = new AiDialogueModuleTestRuntimeStateService();
        var dialogueService = new AiDialogueModuleTestDialogueService();
        var module = new AiDialogueModule(runtimeState, dialogueService);
        var npc = CreateProxy((Serial)0x100u, "Lilly");
        var sender = CreateProxy((Serial)0x200u, "Marcus");

        var handled = module.Listener(npc, sender, "hello there");

        Assert.Multiple(
            () =>
            {
                Assert.That(handled, Is.True);
                Assert.That(dialogueService.LastListenerNpc, Is.Not.Null);
                Assert.That(dialogueService.LastListenerNpc!.Id, Is.EqualTo((Serial)0x100u));
                Assert.That(dialogueService.LastSender, Is.Not.Null);
                Assert.That(dialogueService.LastSender!.Id, Is.EqualTo((Serial)0x200u));
                Assert.That(dialogueService.LastText, Is.EqualTo("hello there"));
            }
        );
    }

    private static LuaMobileProxy CreateProxy(Serial serial, string name)
    {
        var mobile = new UOMobileEntity
        {
            Id = serial,
            Name = name,
            MapId = 1,
            Location = new(100, 100, 0)
        };

        return new(
            mobile,
            new AiDialogueModuleTestSpeechService(),
            new AiDialogueModuleTestGameNetworkSessionService()
        );
    }
}
