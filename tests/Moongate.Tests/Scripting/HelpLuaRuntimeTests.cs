using System.Net.Sockets;
using DryIoc;
using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Network.Client;
using Moongate.Network.Packets.Outgoing.UI;
using Moongate.Scripting.Services;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Modules;
using Moongate.Server.Services.Scripting;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Ids;

namespace Moongate.Tests.Scripting;

public sealed class HelpLuaRuntimeTests
{
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
}
