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

            // Run and Stop can independently observe the same lifetime fault.
            // Retain distinct cleanup failures without reporting that original fault twice.
            List<Exception> failures = [primaryFailure];
            AddShutdownFailures(shutdownFailure, primaryFailure, failures);

            if (failures.Count > 1)
            {
                throw new AggregateException(failures);
            }
        }

        if (primaryFailure is not null)
        {
            ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        }
    }

    private static void AddShutdownFailures(Exception failure, Exception primaryFailure, List<Exception> failures)
    {
        if (ReferenceEquals(failure, primaryFailure)) return;

        if (failure is AggregateException { InnerExceptions.Count: > 0 } aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
            {
                AddShutdownFailures(inner, primaryFailure, failures);
            }
        }
        else
        {
            failures.Add(failure);
        }
    }
}
