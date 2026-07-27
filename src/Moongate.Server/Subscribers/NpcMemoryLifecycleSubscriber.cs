using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Abstractions.Interfaces.Events;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Subscribers;

/// <summary>Drops an NPC's durable memory when the mobile is deleted, so no orphan rows remain.</summary>
public sealed class NpcMemoryLifecycleSubscriber : IEventSubscriberRegistration
{
    private readonly INpcMemoryService _memory;

    public NpcMemoryLifecycleSubscriber(INpcMemoryService memory)
    {
        _memory = memory;
    }

    public Task OnMobileDeleted(MobileDeletedEvent message, CancellationToken cancellationToken)
    {
        _memory.Forget(message.Mobile.Id);

        return Task.CompletedTask;
    }

    public void Subscribe(IEventBus eventBus)
    {
        eventBus.Subscribe<MobileDeletedEvent>(OnMobileDeleted);
    }
}
