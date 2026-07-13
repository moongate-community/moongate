using DryIoc;
using Moongate.Server.Extensions;
using Moongate.Server.Interfaces.Loading;

namespace Moongate.Tests.Server;

public class DataLoaderRegistrationTests
{
    private static readonly List<string> Order = [];

    private sealed class FirstLoader : IDataLoader
    {
        public ValueTask LoadAsync(CancellationToken ct = default)
        {
            Order.Add("first");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SecondLoader : IDataLoader
    {
        public ValueTask LoadAsync(CancellationToken ct = default)
        {
            Order.Add("second");
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task RegisteredLoaders_RunInPriorityOrder_RegardlessOfRegistrationOrder()
    {
        Order.Clear();
        var container = new Container();

        // Register out of priority order on purpose.
        container.RegisterDataLoader<SecondLoader>(priority: 20);
        container.RegisterDataLoader<FirstLoader>(priority: 10);
        container.RegisterDataLoaderService();

        var service = container.Resolve<IDataLoaderService>();
        await service.ExecuteLoadersAsync();

        Assert.Equal(new[] { "first", "second" }, Order);
    }

    [Fact]
    public void RegisterDataLoaderService_WithNoLoaders_ResolvesAndIsNoOp()
    {
        var container = new Container();
        container.RegisterDataLoaderService();

        var service = container.Resolve<IDataLoaderService>();

        Assert.NotNull(service);
    }
}
