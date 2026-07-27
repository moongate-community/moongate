using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Subscribers;

namespace Moongate.Tests.Server.AI;

public class NpcMemoryLifecycleSubscriberTests
{
    [Fact]
    public async Task OnMobileDeleted_ForgetsThatMobilesMemory()
    {
        var memory = new RecordingForget();
        var subscriber = new NpcMemoryLifecycleSubscriber(memory);

        await subscriber.OnMobileDeleted(new MobileDeletedEvent(new MobileEntity { Id = new(0x7) }), default);

        Assert.Equal(new Serial(0x7), Assert.Single(memory.Forgotten));
    }

    private sealed class RecordingForget : INpcMemoryService
    {
        public List<Serial> Forgotten { get; } = [];

        public IDisposable Begin(BrainContext context)
            => throw new NotSupportedException();

        public bool Set(string key, NpcMemoryValue value)
            => throw new NotSupportedException();

        public NpcMemoryValue? Get(string key)
            => throw new NotSupportedException();

        public bool Delete(string key)
            => throw new NotSupportedException();

        public IReadOnlyDictionary<string, NpcMemoryValue> All()
            => throw new NotSupportedException();

        public void Forget(Serial mobileId)
            => Forgotten.Add(mobileId);
    }
}
