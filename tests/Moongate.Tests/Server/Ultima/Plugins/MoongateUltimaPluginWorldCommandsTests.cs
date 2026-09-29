using DryIoc;
using Moongate.Core.Directories;
using Moongate.Persistence.Extensions;
using Moongate.Server.Core.Commands;
using Moongate.Server.Core.Data.Commands;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Commands;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Services.Hosting;
using Moongate.Server.Services.Timing;
using Moongate.Server.Ultima;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.General;
using Moongate.Tests.TestSupport.Config;
using Moongate.Tests.TestSupport.Directories;
using Moongate.Tests.TestSupport.Ultima.Commands;
using Moongate.Tests.TestSupport.Ultima.Speech;

namespace Moongate.Tests.Server.Ultima.Plugins;

public sealed class MoongateUltimaPluginWorldCommandsTests
{
    [Theory]
    [InlineData(ServerMode.Game, "save")]
    [InlineData(ServerMode.Game, "broadcast")]
    [InlineData(ServerMode.Game, "shutdown")]
    [InlineData(ServerMode.Standalone, "save")]
    [InlineData(ServerMode.Standalone, "broadcast")]
    [InlineData(ServerMode.Standalone, "shutdown")]
    public async Task Register_WorldCommandsResolveAndRequireAdministratorFromConsoleOrGame(ServerMode mode, string name)
    {
        using var root = new TemporaryDirectory();
        using var container = new Container();
        container.RegisterInstance(TestConfigDocuments.Empty(root.Path));
        container.RegisterInstance(new DirectoriesConfig(root.Path, ["data"]));
        container.RegisterInstance(mode);
        container.RegisterMoongatePersistence(new());
        new MoongateUltimaPlugin().Register(container);

        var registrations = container.Resolve<CommandRegistry>().Registrations;
        Assert.True(registrations.TryGetValue(name, out var registration));
        Assert.Equal(CommandSourceType.Console | CommandSourceType.InGame, registration.Definition.Source);
        Assert.Equal(AccountType.Administrator, registration.Definition.MinimumAccountType);

        await using var fixture = await BroadcastFixture.CreateAsync();
        await fixture.AddAsync(1);
        var saves = new ControlledWorldSaveService();
        saves.Completion.SetResult();
        container.RegisterInstance<IWorldSaveService>(saves);
        var shutdown = new ServerShutdownService();
        container.RegisterInstance<IServerShutdownService>(shutdown);
        container.RegisterInstance<ITimerService>(new TimerWheelService(new(), TimeProvider.System));
        container.RegisterInstance<IGameLoopService>(fixture.Network.Loop);
        container.RegisterInstance<ISessionService>(fixture.Sessions);
        container.RegisterInstance<IPacketSendService>(fixture.Sender);
        container.RegisterInstance<IMobileService>(fixture.Mobiles, ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        var context = name == "broadcast"
            ? new CommandContext("broadcast Maintenance soon", name, ["Maintenance", "soon"], CommandSourceType.Console, null)
            : new CommandContext(name, name, [], CommandSourceType.Console, null);

        await registration.Bind(container)(context);

        var packet = Assert.IsType<UnicodeSpeechMessagePacket>(Assert.Single(fixture.Sender.Sent));
        if (name == "save")
        {
            Assert.StartsWith("world saved in ", packet.Text);
        }
        else if (name == "shutdown")
        {
            Assert.Equal("Server is shutting down now.", packet.Text);
            Assert.True(shutdown.Requested.IsCompletedSuccessfully);
        }
        else
        {
            Assert.Equal("Maintenance soon", packet.Text);
        }
    }

    [Fact]
    public void Register_LoginRoleDoesNotExposeWorldCommands()
    {
        using var root = new TemporaryDirectory();
        using var container = new Container();
        container.RegisterInstance(TestConfigDocuments.Empty(root.Path));
        container.RegisterInstance(new DirectoriesConfig(root.Path, ["data"]));
        container.RegisterInstance(ServerMode.Login);
        container.RegisterMoongatePersistence(new());

        new MoongateUltimaPlugin().Register(container);

        var registrations = container.Resolve<CommandRegistry>().Registrations;
        Assert.False(registrations.ContainsKey("save"));
        Assert.False(registrations.ContainsKey("broadcast"));
        Assert.False(registrations.ContainsKey("shutdown"));
        Assert.False(container.IsRegistered<IBroadcastService>());
    }
}
