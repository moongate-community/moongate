using DryIoc;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Bootstrap;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Extensions;
using Moongate.Tests.Support.Events;
using Moongate.Tests.Support.Server;
using Moongate.Tests.TestSupport.Persistence;

namespace Moongate.Tests.Integration.Persistence;

[Collection(PostgresTestCollection.Name)]
public sealed class PersistenceLifecycleEventTests
{
    [Fact]
    public async Task StartAsync_CanceledReadyObserver_PublishesStoppedWithoutStartingServices()
    {
        await using var fixture = await HostPersistenceFixture.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var events = new List<string>();
        var serviceStarted = false;
        fixture.Container.OnEvent<PersistenceReadyEvent>((_, _) =>
            {
                events.Add("ready");
                cancellation.Cancel();

                return Task.CompletedTask;
            }
        );
        fixture.Container.OnEvent<PersistenceStoppedEvent>((_, _) =>
            {
                events.Add("stopped");

                return Task.CompletedTask;
            }
        );
        fixture.Container.AddMoongateService(
            new CallbackStartupService(
                () =>
                {
                    serviceStarted = true;

                    return Task.CompletedTask;
                },
                () => Task.CompletedTask
            )
        );
        var bootstrap = new MoongateServerBootstrap(fixture.Container, cancellation.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(bootstrap.StartAsync);
        await bootstrap.StopAsync();

        Assert.Equal(["ready", "stopped"], events);
        Assert.False(serviceStarted);
        Assert.True(fixture.Container.IsDisposed);
    }

    [Fact]
    public async Task StartAndStopAsync_EventsObserveUsablePersistenceThenDisposedOwnerExactlyOnce()
    {
        await using var fixture = await HostPersistenceFixture.CreateAsync();
        fixture.RegisterEntity();
        using var cancellation = new CancellationTokenSource();
        var container = fixture.Container;
        var owner = fixture.Owner;
        var events = new List<string>();
        Exception? stoppedAccessError = null;
        var containerAliveAtStopped = false;
        var stoppedTokenCanBeCanceled = true;
        container.RegisterInstance(new RecordingDisposable(events, "container:dispose"));
        container.OnEvent<PersistenceReadyEvent>(async (_, token) =>
            {
                await container.Resolve<IDataAccess<TestEntity>>().UpsertAsync(new() { Id = new(1), Name = "ready" }, token);
                events.Add("persistence:ready");
            }
        );
        container.OnEvent<MoongateStartedEvent>((_, _) =>
            {
                events.Add("server:started");

                return Task.CompletedTask;
            }
        );
        container.OnEvent<MoongateStoppedEvent>((_, _) =>
            {
                events.Add("server:stopped");

                return Task.CompletedTask;
            }
        );
        container.OnEvent<PersistenceStoppedEvent>(async (_, token) =>
            {
                stoppedAccessError = await Record.ExceptionAsync(() => owner.SaveAllAsync());
                containerAliveAtStopped = !container.IsDisposed;
                stoppedTokenCanBeCanceled = token.CanBeCanceled;
                events.Add("persistence:stopped");
            }
        );
        container.AddMoongateService(
            new CallbackStartupService(
                async () =>
                {
                    Assert.Equal("ready", (await container.Resolve<IDataAccess<TestEntity>>().GetByIdAsync(new(1)))?.Name);
                    events.Add("service:start");
                },
                () =>
                {
                    events.Add("service:stop");

                    return Task.CompletedTask;
                }
            )
        );
        var bootstrap = new MoongateServerBootstrap(container, cancellation.Token);

        await Task.WhenAll(bootstrap.StartAsync(), bootstrap.StartAsync());
        cancellation.Cancel();
        await Task.WhenAll(bootstrap.StopAsync(), bootstrap.StopAsync());

        Assert.Equal(
            [
                "persistence:ready", "service:start", "server:started", "service:stop", "server:stopped",
                "persistence:stopped",
                "container:dispose"
            ],
            events
        );
        Assert.IsType<ObjectDisposedException>(stoppedAccessError);
        Assert.True(containerAliveAtStopped);
        Assert.False(stoppedTokenCanBeCanceled);
        Assert.True(container.IsDisposed);
    }

    [Fact]
    public async Task StartAndStopAsync_NoPersistence_DoesNotPublishPersistenceEvents()
    {
        using var container = new Container();
        var count = 0;
        container.OnEvent<PersistenceReadyEvent>((_, _) =>
            {
                count++;

                return Task.CompletedTask;
            }
        );
        container.OnEvent<PersistenceStoppedEvent>((_, _) =>
            {
                count++;

                return Task.CompletedTask;
            }
        );
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);

        await bootstrap.StartAsync();
        await bootstrap.StopAsync();

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task StartAsync_PersistenceInitializationFails_DoesNotPublishPersistenceEvents()
    {
        await using var fixture = await HostPersistenceFixture.CreateAsync(false);
        fixture.RegisterEntity();
        var count = 0;
        fixture.Container.OnEvent<PersistenceReadyEvent>((_, _) =>
            {
                count++;

                return Task.CompletedTask;
            }
        );
        fixture.Container.OnEvent<PersistenceStoppedEvent>((_, _) =>
            {
                count++;

                return Task.CompletedTask;
            }
        );
        var bootstrap = new MoongateServerBootstrap(fixture.Container, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(bootstrap.StartAsync);
        await bootstrap.StopAsync();

        Assert.Equal(0, count);
        Assert.True(fixture.Container.IsDisposed);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task ServiceFailure_AfterPersistenceInitialization_StillPublishesStopped(bool failOnStart)
    {
        await using var fixture = await HostPersistenceFixture.CreateAsync();
        var events = new List<string>();
        var failure = new IOException("service failure");
        fixture.Container.OnEvent<PersistenceReadyEvent>((_, _) =>
            {
                events.Add("ready");

                return Task.CompletedTask;
            }
        );
        fixture.Container.OnEvent<PersistenceStoppedEvent>((_, _) =>
            {
                events.Add("stopped");

                return Task.CompletedTask;
            }
        );
        fixture.Container.AddMoongateService(
            new CallbackStartupService(
                () => failOnStart ? Task.FromException(failure) : Task.CompletedTask,
                () => failOnStart ? Task.CompletedTask : Task.FromException(failure)
            )
        );
        var bootstrap = new MoongateServerBootstrap(fixture.Container, CancellationToken.None);

        if (failOnStart)
        {
            Assert.Same(failure, await Record.ExceptionAsync(bootstrap.StartAsync));
            await bootstrap.StopAsync();
        }
        else
        {
            await bootstrap.StartAsync();
            Assert.Same(failure, await Record.ExceptionAsync(bootstrap.StopAsync));
        }

        Assert.Equal(["ready", "stopped"], events);
        Assert.True(fixture.Container.IsDisposed);
    }

    [Fact]
    public async Task StopAsync_FaultingStoppedObserver_DoesNotPreventOtherObserversOrContainerDisposal()
    {
        await using var fixture = await HostPersistenceFixture.CreateAsync();
        var count = 0;
        fixture.Container.OnEvent<PersistenceStoppedEvent>((_, _) => throw new IOException("observer failure"));
        fixture.Container.OnEvent<PersistenceStoppedEvent>((_, _) =>
            {
                count++;

                return Task.CompletedTask;
            }
        );
        var bootstrap = new MoongateServerBootstrap(fixture.Container, CancellationToken.None);

        await bootstrap.StartAsync();
        await bootstrap.StopAsync();

        Assert.Equal(1, count);
        Assert.True(fixture.Container.IsDisposed);
    }
}
