using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Services.AI;
using Moongate.Tests.Support;

namespace Moongate.Tests.Server.AI;

public class NpcMemoryServiceTests
{
    [Fact]
    public void Set_Then_Get_RoundTripsScalarTypes()
    {
        var (service, persistence) = Build();
        var npc = AddMobile(persistence, 0x1);

        using (service.Begin(Context(npc)))
        {
            Assert.True(service.Set("name", NpcMemoryValue.FromString("Bob")));
            Assert.True(service.Set("count", NpcMemoryValue.FromNumber(3)));
            Assert.True(service.Set("hostile", NpcMemoryValue.FromBoolean(true)));

            Assert.Equal("Bob", service.Get("name")!.ToScalar());
            Assert.Equal(3d, service.Get("count")!.ToScalar());
            Assert.Equal(true, service.Get("hostile")!.ToScalar());
            Assert.Null(service.Get("missing"));
        }

        var stored = persistence.Store<NpcMemoryEntity>().GetById(npc.Id)!;
        Assert.Equal(3, stored.Memory.Count);
    }

    [Fact]
    public void Set_SurvivesAFreshServiceOverTheSameStore()
    {
        var (service, persistence) = Build();
        var npc = AddMobile(persistence, 0x1);
        using (service.Begin(Context(npc)))
        {
            service.Set("seen", NpcMemoryValue.FromNumber(1));
        }

        var reopened = new NpcMemoryService(persistence, new StubLoopAffinity());
        using (reopened.Begin(Context(npc)))
        {
            Assert.Equal(1d, reopened.Get("seen")!.ToScalar());
        }
    }

    [Fact]
    public void Set_UnknownMobile_ReturnsFalse()
    {
        var (service, persistence) = Build();
        var ghost = new MobileEntity { Id = new(0x99), Name = "ghost" };

        using (service.Begin(Context(ghost)))
        {
            Assert.False(service.Set("k", NpcMemoryValue.FromString("v")));
        }

        Assert.Null(persistence.Store<NpcMemoryEntity>().GetById(new(0x99)));
    }

    [Fact]
    public void Set_ExceedingBounds_ReturnsFalse()
    {
        var (service, persistence) = Build();
        var npc = AddMobile(persistence, 0x1);

        using (service.Begin(Context(npc)))
        {
            Assert.False(service.Set(new string('k', 65), NpcMemoryValue.FromString("v")));
            Assert.False(service.Set("k", NpcMemoryValue.FromString(new string('v', 257))));
            for (var i = 0; i < 128; i++)
            {
                Assert.True(service.Set("k" + i, NpcMemoryValue.FromNumber(i)));
            }

            Assert.False(service.Set("k128", NpcMemoryValue.FromNumber(0)));
            Assert.True(service.Set("k0", NpcMemoryValue.FromNumber(99)));
        }
    }

    [Fact]
    public void Delete_And_Forget()
    {
        var (service, persistence) = Build();
        var npc = AddMobile(persistence, 0x1);
        using (service.Begin(Context(npc)))
        {
            service.Set("a", NpcMemoryValue.FromNumber(1));
            Assert.True(service.Delete("a"));
            Assert.False(service.Delete("a"));
            Assert.Null(service.Get("a"));
            service.Set("b", NpcMemoryValue.FromNumber(2));
        }

        service.Forget(npc.Id);
        Assert.Null(persistence.Store<NpcMemoryEntity>().GetById(npc.Id));
    }

    [Fact]
    public void Action_WithoutActiveContext_Throws()
    {
        var (service, _) = Build();

        var exception = Assert.Throws<InvalidOperationException>(() => service.Get("k"));
        Assert.Contains("brain tick", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Begin_RestoresPreviousContextOnDispose()
    {
        var (service, persistence) = Build();
        var a = AddMobile(persistence, 0x1);
        var b = AddMobile(persistence, 0x2);

        using (service.Begin(Context(a)))
        {
            using (service.Begin(Context(b)))
            {
                service.Set("x", NpcMemoryValue.FromNumber(1));
            }

            service.Set("y", NpcMemoryValue.FromNumber(2));
        }

        Assert.Equal(1, persistence.Store<NpcMemoryEntity>().GetById(b.Id)!.Memory.Count);
        Assert.Equal(1, persistence.Store<NpcMemoryEntity>().GetById(a.Id)!.Memory.Count);
        Assert.Throws<InvalidOperationException>(() => service.Get("z"));
    }

    private static (NpcMemoryService Service, FakePersistenceService Persistence) Build()
    {
        var persistence = new FakePersistenceService();
        return (new NpcMemoryService(persistence, new StubLoopAffinity()), persistence);
    }

    private static MobileEntity AddMobile(FakePersistenceService persistence, uint id)
    {
        var mobile = new MobileEntity { Id = new(id), Name = "npc-" + id };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).AsTask().Wait();
        return mobile;
    }

    private static BrainContext Context(MobileEntity owner)
        => new(
            DateTimeOffset.UtcNow,
            new BrainMobileSnapshot(
                owner.Id,
                owner.Name,
                false,
                owner.MapId,
                owner.Position,
                owner.Hits,
                owner.HitsMax,
                owner.Warmode,
                owner.CombatantId,
                owner.Criminal,
                owner.Kills
            ),
            owner.MapId,
            owner.Position,
            []
        );
}
