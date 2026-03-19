using System.Net.Sockets;
using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.UI;
using Moongate.Network.Packets.Outgoing.UI;
using Moongate.Scripting.Services;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Modules;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Scripting;

public sealed class HelpLuaRuntimeTests
{
    private sealed class HelpLuaRuntimeTicketServiceStub : IHelpTicketService
    {
        public HelpTicketCategory? LastCategory { get; private set; }

        public string? LastMessage { get; private set; }

        public long LastSessionId { get; private set; }

        public Task<HelpTicketEntity?> CreateTicketAsync(
            long sessionId,
            HelpTicketCategory category,
            string message,
            CancellationToken cancellationToken = default
        )
        {
            _ = cancellationToken;
            LastSessionId = sessionId;
            LastCategory = category;
            LastMessage = message;

            return Task.FromResult<HelpTicketEntity?>(
                new()
                {
                    Id = (Serial)(Serial.ItemOffset + 90),
                    SenderCharacterId = (Serial)0x00000044u,
                    SenderAccountId = (Serial)0x00000010u,
                    Category = category,
                    Message = message,
                    MapId = 0,
                    Location = new(1443, 1692, 0),
                    Status = HelpTicketStatus.Open,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastUpdatedAtUtc = DateTime.UtcNow
                }
            );
        }

        public Task<IReadOnlyList<HelpTicketEntity>> GetAllTicketsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HelpTicketEntity>>([]);

        public Task<(IReadOnlyList<HelpTicketEntity> Items, int TotalCount)> GetTicketsForAdminAsync(
            int page,
            int pageSize,
            HelpTicketStatus? status,
            HelpTicketCategory? category,
            Serial? assignedToAccountId,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult<(IReadOnlyList<HelpTicketEntity>, int)>(([], 0));

        public Task<HelpTicketEntity?> GetTicketByIdAsync(Serial ticketId, CancellationToken cancellationToken = default)
            => Task.FromResult<HelpTicketEntity?>(null);

        public Task<HelpTicketEntity?> AssignToAccountAsync(
            Serial ticketId,
            Serial assignedToAccountId,
            Serial? assignedToCharacterId,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult<HelpTicketEntity?>(null);

        public Task<HelpTicketEntity?> UpdateStatusAsync(
            Serial ticketId,
            HelpTicketStatus status,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult<HelpTicketEntity?>(null);

        public Task<IReadOnlyList<HelpTicketEntity>> GetOpenTicketsForAccountAsync(
            Serial senderAccountId,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult<IReadOnlyList<HelpTicketEntity>>([]);

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;
    }

    [Test]
    public async Task StartAsync_WithHelpScripts_ShouldOpenCompressedHelpGump()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = temp.Path;
        Directory.CreateDirectory(Path.Combine(scriptsDir, "interaction"));
        Directory.CreateDirectory(Path.Combine(scriptsDir, "gumps"));
        Directory.CreateDirectory(luarcDir);

        var repoRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
        File.Copy(
            Path.Combine(repoRoot, "moongate_data", "scripts", "interaction", "help.lua"),
            Path.Combine(scriptsDir, "interaction", "help.lua")
        );
        File.Copy(
            Path.Combine(repoRoot, "moongate_data", "scripts", "gumps", "help.lua"),
            Path.Combine(scriptsDir, "gumps", "help.lua")
        );
        await File.WriteAllTextAsync(
            Path.Combine(scriptsDir, "init.lua"),
            "require(\"interaction.init\")\n"
        );
        await File.WriteAllTextAsync(
            Path.Combine(scriptsDir, "interaction", "init.lua"),
            "require(\"interaction.help\")\n"
        );

        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            CharacterId = (Serial)0x00000044u
        };
        sessionService.Add(session);

        var container = new Container();
        container.RegisterInstance<IOutgoingPacketQueue>(queue);
        container.RegisterInstance<IGameNetworkSessionService>(sessionService);
        container.RegisterInstance<IGumpScriptDispatcherService>(new GumpScriptDispatcherService());

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(GumpModule))],
            container,
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();

        var result = service.ExecuteFunction(
            $"(function() return on_help_request({session.SessionId}, {(uint)session.CharacterId}) end)()"
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.EqualTo(true));
                Assert.That(queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(outbound.Packet, Is.TypeOf<CompressedGumpPacket>());
                Assert.That(((CompressedGumpPacket)outbound.Packet).GumpId, Is.EqualTo(0xB900u));
            }
        );
    }

    [Test]
    public async Task StartAsync_WithHelpScripts_ShouldOpenQuestionTextEntryAndSubmitTicket()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = temp.Path;
        Directory.CreateDirectory(Path.Combine(scriptsDir, "interaction"));
        Directory.CreateDirectory(Path.Combine(scriptsDir, "gumps"));
        Directory.CreateDirectory(luarcDir);

        var repoRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
        File.Copy(
            Path.Combine(repoRoot, "moongate_data", "scripts", "interaction", "help.lua"),
            Path.Combine(scriptsDir, "interaction", "help.lua")
        );
        File.Copy(
            Path.Combine(repoRoot, "moongate_data", "scripts", "gumps", "help.lua"),
            Path.Combine(scriptsDir, "gumps", "help.lua")
        );
        await File.WriteAllTextAsync(
            Path.Combine(scriptsDir, "init.lua"),
            "require(\"interaction.init\")\n"
        );
        await File.WriteAllTextAsync(
            Path.Combine(scriptsDir, "interaction", "init.lua"),
            "require(\"interaction.help\")\n"
        );

        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var sessionService = new FakeGameNetworkSessionService();
        var gumpDispatcher = new GumpScriptDispatcherService();
        var ticketService = new HelpLuaRuntimeTicketServiceStub();
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            AccountId = (Serial)0x00000010u,
            CharacterId = (Serial)0x00000044u
        };
        sessionService.Add(session);

        var container = new Container();
        container.RegisterInstance<IOutgoingPacketQueue>(queue);
        container.RegisterInstance<IGameNetworkSessionService>(sessionService);
        container.RegisterInstance<IGumpScriptDispatcherService>(gumpDispatcher);
        container.RegisterInstance<IHelpTicketService>(ticketService);

        var service = new LuaScriptEngineService(
            dirs,
            [new(typeof(GumpModule)), new(typeof(HelpTicketsModule))],
            container,
            new(luarcDir, scriptsDir, "0.1.0"),
            []
        );

        await service.StartAsync();

        _ = service.ExecuteFunction(
            $"(function() return on_help_request({session.SessionId}, {(uint)session.CharacterId}) end)()"
        );
        Assert.That(queue.TryDequeue(out var categoryOutbound), Is.True);
        Assert.That(categoryOutbound.Packet, Is.TypeOf<CompressedGumpPacket>());

        var selectQuestionPacket = new GumpMenuSelectionPacket();
        Assert.That(
            selectQuestionPacket.TryParse(BuildGumpResponsePacket((uint)session.CharacterId, 0xB900, 1)),
            Is.True
        );
        Assert.That(gumpDispatcher.TryDispatch(session, selectQuestionPacket), Is.True);
        Assert.That(queue.TryDequeue(out var textOutbound), Is.True);
        Assert.That(textOutbound.Packet, Is.TypeOf<CompressedGumpPacket>());

        var submitPacket = new GumpMenuSelectionPacket();
        Assert.That(
            submitPacket.TryParse(
                BuildGumpResponsePacket(
                    (uint)session.CharacterId,
                    0xB901,
                    1,
                    new Dictionary<ushort, string> { [1] = "I am stuck behind the innkeeper counter." }
                )
            ),
            Is.True
        );
        Assert.That(gumpDispatcher.TryDispatch(session, submitPacket), Is.True);

        Assert.Multiple(
            () =>
            {
                Assert.That(ticketService.LastSessionId, Is.EqualTo(session.SessionId));
                Assert.That(ticketService.LastCategory, Is.EqualTo(HelpTicketCategory.Question));
                Assert.That(ticketService.LastMessage, Is.EqualTo("I am stuck behind the innkeeper counter."));
            }
        );
    }

    private static byte[] BuildGumpResponsePacket(
        uint serial,
        uint gumpId,
        uint buttonId,
        IReadOnlyDictionary<ushort, string>? textEntries = null
    )
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, true);

        bw.Write((byte)0xB1);
        bw.Write((ushort)0);
        WriteUInt32BE(bw, serial);
        WriteUInt32BE(bw, gumpId);
        WriteUInt32BE(bw, buttonId);
        WriteInt32BE(bw, 0);

        var entries = textEntries ?? new Dictionary<ushort, string>();
        WriteInt32BE(bw, entries.Count);

        foreach (var (id, text) in entries)
        {
            var value = text ?? string.Empty;
            var textBytes = System.Text.Encoding.BigEndianUnicode.GetBytes(value);
            WriteUInt16BE(bw, id);
            WriteUInt16BE(bw, (ushort)value.Length);
            bw.Write(textBytes);
        }

        bw.Flush();
        var bytes = ms.ToArray();
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(1, 2), (ushort)bytes.Length);
        return bytes;
    }

    private static void WriteInt32BE(BinaryWriter writer, int value)
        => writer.Write(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(value));

    private static void WriteUInt16BE(BinaryWriter writer, ushort value)
        => writer.Write(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(value));

    private static void WriteUInt32BE(BinaryWriter writer, uint value)
        => writer.Write(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(value));
}
