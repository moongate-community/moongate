using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Subscribers;

namespace Moongate.Tests.Server.AI;

public class NpcMemoryLifecycleSubscriberTests
{
    private sealed class RecordingForget : INpcMemoryService
    {
        public List<Serial> Forgotten { get; } = [];

        public IReadOnlyDictionary<string, NpcMemoryValue> All()
            => throw new NotSupportedException();

        public IDisposable Begin(BrainContext context)
            => throw new NotSupportedException();

        public bool Delete(string key)
            => throw new NotSupportedException();

        public void Forget(Serial mobileId)
            => Forgotten.Add(mobileId);

        public NpcMemoryValue? Get(string key)
            => throw new NotSupportedException();

        public bool Set(string key, NpcMemoryValue value)
            => throw new NotSupportedException();
    }

    [Fact]
    public async Task OnMobileDeleted_ForgetsThatMobilesMemory()
    {
        var memory = new RecordingForget();
        var subscriber = new NpcMemoryLifecycleSubscriber(memory);

        await subscriber.OnMobileDeleted(new(new() { Id = new(0x7) }), default);

        Assert.Equal(new(0x7), Assert.Single(memory.Forgotten));
    }
}
