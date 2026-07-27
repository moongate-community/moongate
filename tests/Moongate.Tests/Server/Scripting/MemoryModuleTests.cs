using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Scripting;

namespace Moongate.Tests.Server.Scripting;

public class MemoryModuleTests
{
    [Fact]
    public void Set_ConvertsLuaScalarsToTypedValues()
    {
        var svc = new RecordingMemoryService { Result = true };
        var module = new MemoryModule(svc);

        Assert.True(module.Set("s", "hi"));
        Assert.True(module.Set("n", 4d));
        Assert.True(module.Set("b", true));

        Assert.Equal(NpcMemoryValue.FromString("hi"), svc.Stored["s"]);
        Assert.Equal(NpcMemoryValue.FromNumber(4), svc.Stored["n"]);
        Assert.Equal(NpcMemoryValue.FromBoolean(true), svc.Stored["b"]);
    }

    [Fact]
    public void Set_UnsupportedType_ReturnsFalseWithoutCallingService()
    {
        var svc = new RecordingMemoryService { Result = true };
        var module = new MemoryModule(svc);

        Assert.False(module.Set("t", new object()));
        Assert.Empty(svc.Stored);
    }

    [Fact]
    public void Get_ReturnsUnderlyingScalarOrNull()
    {
        var svc = new RecordingMemoryService();
        svc.Stored["n"] = NpcMemoryValue.FromNumber(7);
        var module = new MemoryModule(svc);

        Assert.Equal(7d, module.Get("n"));
        Assert.Null(module.Get("missing"));
    }

    [Fact]
    public void All_ProjectsScalarsByKey()
    {
        var svc = new RecordingMemoryService();
        svc.Stored["s"] = NpcMemoryValue.FromString("x");
        svc.Stored["b"] = NpcMemoryValue.FromBoolean(false);
        var module = new MemoryModule(svc);

        var dict = module.All().ToDictionary();

        Assert.Equal("x", dict["s"]);
        Assert.Equal(false, dict["b"]);
    }

    private sealed class RecordingMemoryService : INpcMemoryService
    {
        public bool Result { get; set; }

        public Dictionary<string, NpcMemoryValue> Stored { get; } = new(StringComparer.Ordinal);

        public IDisposable Begin(BrainContext context)
            => new Noop();

        public bool Set(string key, NpcMemoryValue value)
        {
            Stored[key] = value;
            return Result;
        }

        public NpcMemoryValue? Get(string key)
            => Stored.TryGetValue(key, out var value) ? value : null;

        public bool Delete(string key)
            => Stored.Remove(key);

        public IReadOnlyDictionary<string, NpcMemoryValue> All()
            => Stored;

        public void Forget(Serial mobileId)
            => Stored.Clear();

        private sealed class Noop : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
