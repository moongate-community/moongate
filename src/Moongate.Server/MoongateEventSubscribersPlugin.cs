using DryIoc;
using Moongate.Server.Abstractions.Extensions;
using Moongate.Server.Abstractions.Interfaces.Events;
using Moongate.Server.Services.Events;
using Moongate.Server.Subscribers;
using SquidStd.Abstractions.Extensions.Services;
using SquidStd.Core.Utils;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.Server;

/// <summary>Registers Moongate's domain event subscribers and the service that wires them to the bus.</summary>
public class MoongateEventSubscribersPlugin : ISquidStdPlugin
{
    public PluginMetadata Metadata
        => new()
        {
            Id = "moongate.eventsubscribers.plugin",
            Version = new(VersionUtils.GetVersion(typeof(MoongateEventSubscribersPlugin).Assembly)),
            Author = "squid",
            Name = "Moongate Event Subscribers",
            Description = "Domain event subscriber registrations"
        };

    public void Configure(IContainer container, PluginContext context)
    {
        container.RegisterEventSubscriber<PaperdollSubscriber>();
        container.RegisterEventSubscriber<ContainerSubscriber>();

        container.RegisterStdService<IEventSubscriberService, EventSubscriberService>();
    }
}
