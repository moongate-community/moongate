using DryIoc;
using Moongate.Server.Bootstrap;
using Moongate.Server.Core.Data.Events;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Admin;
using Moongate.Tests.TestSupport.Admin;

namespace Moongate.Tests.Server.Admin;

public sealed class AdminBootstrapTests
{
    [Fact]
    public async Task Bootstrap_StartedSubscribersAndStopping_AreOutsideRequestAdmission()
    {
        var container = new Container();
        var api = new RecordingAdminApiService();
        container.AddMoongateService<IAdminApiService>(api, 110);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stoppedAcceptingBeforeEvent = false;
        container.OnEvent<MoongateStartedEvent>(
            async (_, _) =>
            {
                entered.SetResult();
                await release.Task;
            }
        );
        container.OnEvent<MoongateStoppingEvent>(
            (_, _) =>
            {
                stoppedAcceptingBeforeEvent = !api.Accepting;

                return Task.CompletedTask;
            }
        );
        var bootstrap = new MoongateServerBootstrap(container, CancellationToken.None);
        var start = bootstrap.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(api.Started);
        Assert.False(api.Accepting);
        release.SetResult();
        await start;
        Assert.True(api.Accepting);
        await bootstrap.StopAsync();
        Assert.True(stoppedAcceptingBeforeEvent);
        Assert.False(api.Started);
    }
}
