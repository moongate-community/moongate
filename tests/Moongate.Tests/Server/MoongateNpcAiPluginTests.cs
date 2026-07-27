using DryIoc;
using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Scripting;
using Moongate.Scripting.AI;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Plugins;
using Moongate.Server.Services.AI;
using Moongate.Server.Services.World;
using Moongate.Server.Subscribers;
using Moongate.Tests.Support;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;
using SquidStd.Core.Data.Bootstrap;
using SquidStd.Core.Directories;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Tests.Server;

public sealed class MoongateNpcAiPluginTests
{
    [Fact]
    public void Configure_RegistersAiServicesAndSubscribers()
    {
        using var container = new Container();

        new MoongateNpcAiPlugin().Configure(container, new PluginContext());

        Assert.IsType<NpcAiMetrics>(container.Resolve<INpcAiMetrics>());
        Assert.True(container.IsRegistered<ISectorActivityService>());
        Assert.True(container.IsRegistered<IAiActionService>());
        Assert.True(container.IsRegistered<INpcMemoryService>());
        Assert.True(container.IsRegistered<NpcBrainContextFactory>());
        Assert.True(container.IsRegistered<INpcBrainScheduler>());
        var subscribers = container.GetServiceRegistrations()
            .Where(registration => registration.ServiceType == typeof(IEventSubscriberRegistration))
            .Select(registration => registration.ImplementationType)
            .ToArray();
        Assert.Equal(
            [
                typeof(SectorActivitySubscriber),
                typeof(NpcBrainLifecycleSubscriber),
                typeof(NpcBrainEventRouter),
                typeof(NpcMemoryLifecycleSubscriber)
            ],
            subscribers
        );
    }

    [Fact]
    public void Configure_ProductionDependencies_ResolvesBrainRuntimeAndHostedAiServices()
    {
        var root = Path.Combine(Path.GetTempPath(), "mg-npc-ai-plugin-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "scripts"));
            using var container = new Container();
            var persistence = new FakePersistenceService();
            var eventBus = new StubEventBus();
            var loopAffinity = new StubLoopAffinity();
            container.RegisterInstance(
                new SquidStdOptions { AppName = "MoongateTests", AppVersion = "1.0.0" }
            );
            container.RegisterInstance(new DirectoriesConfig(root, ["scripts"]));
            container.RegisterInstance(new MoongateConfig());
            container.RegisterInstance<IPersistenceService>(persistence);
            container.RegisterInstance<IEventBus>(eventBus);
            container.RegisterInstance<IGameLoopContext>(new StubGameLoopContext());
            container.RegisterInstance<ILoopAffinity>(loopAffinity);
            container.RegisterInstance<ISessionManager>(new StubSessionManager());
            container.RegisterInstance<ISpatialIndexService>(
                new SpatialIndexService(persistence, loopAffinity, eventBus)
            );
            container.RegisterInstance<IMovementService>(new StubMovementService());
            container.RegisterInstance<IChatService>(new StubChatService());
            container.RegisterInstance(Random.Shared);
            container.RegisterInstance<TimeProvider>(TimeProvider.System);

            new MoongateScriptingPlugin().Configure(container, new PluginContext());
            new MoongateNpcAiPlugin().Configure(container, new PluginContext());

            Assert.IsType<LuaNpcBrainRuntime>(container.Resolve<INpcBrainRuntime>());
            Assert.IsType<SectorActivityService>(container.Resolve<ISectorActivityService>());
            Assert.IsType<AiActionService>(container.Resolve<IAiActionService>());
            Assert.IsType<NpcMemoryService>(container.Resolve<INpcMemoryService>());
            Assert.IsType<NpcBrainScheduler>(container.Resolve<INpcBrainScheduler>());
        }
        finally
        {
            TestDirectoryCleanup.TryDelete(root);
        }
    }

    private sealed class StubMovementService : IMovementService
    {
        public void TryMove(PlayerSession session, DirectionType direction, byte sequence)
        {
        }

        public bool TryMoveNpc(Serial mobileId, DirectionType direction)
            => true;
    }

    private sealed class StubChatService : IChatService
    {
        public void Broadcast(string text, Hue? hue = null)
        {
        }

        public void Say(Moongate.Persistence.Entities.MobileEntity speaker, ChatMessageType type, string text, Hue hue, int range)
        {
        }
    }
}
