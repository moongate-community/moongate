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

public sealed class GmMenuLuaRuntimeTests
{
    [Test]
    public async Task StartAsync_WithGmMenuScripts_ShouldOpenCompressedMenuGumpWithAddDefaultTab()
    {
        using var temp = new TempDirectory();
        var dirs = new DirectoriesConfig(temp.Path, Enum.GetNames<DirectoryType>());
        var scriptsDir = dirs[DirectoryType.Scripts];
        var luarcDir = temp.Path;
        Directory.CreateDirectory(Path.Combine(scriptsDir, "interaction"));
        Directory.CreateDirectory(Path.Combine(scriptsDir, "gumps"));
        Directory.CreateDirectory(Path.Combine(scriptsDir, "gumps", "gm_menu"));
        Directory.CreateDirectory(Path.Combine(scriptsDir, "gumps", "gm_menu", "sections"));
        Directory.CreateDirectory(luarcDir);

        var repoRoot =
            Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
        CopyScript(repoRoot, scriptsDir, "interaction/gm_menu.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/gm_menu.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/gm_menu/constants.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/gm_menu/state.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/gm_menu/ui.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/gm_menu/controller.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/gm_menu/render.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/gm_menu/sections/add.lua");
        CopyScript(repoRoot, scriptsDir, "gumps/gm_menu/sections/travel.lua");

        await File.WriteAllTextAsync(
            Path.Combine(scriptsDir, "init.lua"),
            "require(\"interaction.gm_menu\")\n"
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
            $"(function() return on_gm_menu_request({session.SessionId}, {(uint)session.CharacterId}) end)()"
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(result.Success, Is.True);
                Assert.That(result.Data, Is.EqualTo(true));
                Assert.That(queue.TryDequeue(out var outbound), Is.True);
                Assert.That(outbound.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(outbound.Packet, Is.TypeOf<CompressedGumpPacket>());
                var gump = (CompressedGumpPacket)outbound.Packet;
                Assert.That(gump.GumpId, Is.EqualTo(0xB930u));
                Assert.That(gump.Layout, Does.Contain("{ resizepic 0 0 5054 560 420 }"));
                Assert.That(gump.TextLines, Does.Contain("GM Menu"));
                Assert.That(gump.TextLines, Does.Contain("Add"));
                Assert.That(gump.TextLines, Does.Contain("Travel"));
                Assert.That(gump.TextLines, Does.Contain("Search Items and NPCs"));
            }
        );
    }

    private static void CopyScript(string repoRoot, string scriptsDir, string relativePath)
    {
        var sourcePath = Path.Combine(repoRoot, "moongate_data", "scripts", relativePath);
        var destinationPath = Path.Combine(scriptsDir, relativePath);
        var destinationDirectory = Path.GetDirectoryName(destinationPath);

        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        File.Copy(sourcePath, destinationPath);
    }
}
