using System.Runtime.ExceptionServices;

using Moongate.Server.Core.Interfaces.Bootstrap;

namespace Moongate.Server.Bootstrap.Internal;

internal static class MoongateServerRunner
{
    public static async Task RunAsync(IMoongateServerBootstrap bootstrap)
    {
        Exception? primaryFailure = null;

        try
        {
            await bootstrap.StartAsync().ConfigureAwait(false);
            await bootstrap.RunAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
        }

        try
        {
            await bootstrap.StopAsync().ConfigureAwait(false);
        }
        catch (Exception shutdownFailure)
        {
            if (primaryFailure is null) throw;
            throw new AggregateException(primaryFailure, shutdownFailure);
        }

        if (primaryFailure is not null)
        {
            ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        }
    }
}
