using DryIoc;
using Moongate.Server.Abstractions.Extensions;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Services.AI;
using Moongate.Server.Subscribers;
using SquidStd.Abstractions.Extensions.Services;
using SquidStd.Core.Utils;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.Server.Plugins;

/// <summary>Registers the NPC brain scheduler, player-driven sector activity and lifecycle subscribers.</summary>
public sealed class MoongateNpcAiPlugin : ISquidStdPlugin
{
    public PluginMetadata Metadata
        => new()
        {
            Id = "moongate.npcai.plugin",
            Version = new(VersionUtils.GetVersion(typeof(MoongateNpcAiPlugin).Assembly)),
            Author = "squid",
            Name = "Moongate NPC AI",
            Description = "NPC brain scheduling and lifecycle services"
        };

    public void Configure(IContainer container, PluginContext context)
    {
        container.Register<INpcAiMetrics, NpcAiMetrics>(Reuse.Singleton);
        container.RegisterStdService<ISectorActivityService, SectorActivityService>();
        container.Register<IBrainIntentExecutor, BrainIntentExecutor>(Reuse.Singleton);
        container.Register<IAiActionService, AiActionService>(Reuse.Singleton);
        container.Register<NpcBrainContextFactory>(Reuse.Singleton);
        container.RegisterStdService<INpcBrainScheduler, NpcBrainScheduler>();

        container.RegisterEventSubscriber<SectorActivitySubscriber>();
        container.RegisterEventSubscriber<NpcBrainLifecycleSubscriber>();
        container.RegisterEventSubscriber<NpcBrainEventRouter>();
    }
}
