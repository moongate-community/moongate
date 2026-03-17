using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Services.Scripting;

namespace Moongate.Tests.Server.Services.Scripting;

public sealed class AsyncLuaJobRegistryTests
{
    private sealed class TestHandler : IAsyncLuaJobHandler
    {
        public string Name { get; init; } = string.Empty;

        public Task<Dictionary<string, object?>> ExecuteAsync(
            IReadOnlyDictionary<string, object?> payload,
            CancellationToken cancellationToken
        )
        {
            _ = payload;
            _ = cancellationToken;
            return Task.FromResult(new Dictionary<string, object?>());
        }
    }

    [Test]
    public void TryRegister_ShouldAddHandlerAndResolveByName()
    {
        IAsyncLuaJobRegistry registry = new AsyncLuaJobRegistry();
        var handler = new TestHandler { Name = "echo" };

        var registered = registry.TryRegister(handler);
        var resolved = registry.TryResolve("echo", out var actual);

        Assert.Multiple(
            () =>
            {
                Assert.That(registered, Is.True);
                Assert.That(resolved, Is.True);
                Assert.That(actual, Is.SameAs(handler));
            }
        );
    }

    [Test]
    public void TryRegister_WhenDuplicateName_ShouldRejectSecondHandler()
    {
        IAsyncLuaJobRegistry registry = new AsyncLuaJobRegistry();

        var first = registry.TryRegister(new TestHandler { Name = "echo" });
        var second = registry.TryRegister(new TestHandler { Name = "echo" });

        Assert.Multiple(
            () =>
            {
                Assert.That(first, Is.True);
                Assert.That(second, Is.False);
            }
        );
    }

    [Test]
    public void TryResolve_WhenNameIsUnknown_ShouldReturnFalse()
    {
        IAsyncLuaJobRegistry registry = new AsyncLuaJobRegistry();

        var resolved = registry.TryResolve("missing", out var handler);

        Assert.Multiple(
            () =>
            {
                Assert.That(resolved, Is.False);
                Assert.That(handler, Is.Null);
            }
        );
    }
}
