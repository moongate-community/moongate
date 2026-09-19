using Serilog;
namespace Moongate.Api.Hosting.Internal;
internal static class ApiShutdown
{
    private static readonly ILogger Logger = Log.ForContext(typeof(ApiShutdown));
    public static async Task WaitAsync(Task cleanup, Action forceClose, TimeSpan timeout, TimeProvider clock, Func<int> remaining)
    {
        try { await cleanup.WaitAsync(timeout, clock).ConfigureAwait(false); }
        catch (TimeoutException) when (!cleanup.IsCompleted)
        {
            forceClose();
            throw new TimeoutException($"API shutdown timed out with {remaining()} owned connection(s) still completing.");
        }
    }
    public static async Task ObserveAsync(Task cleanup)
    {
        try { await cleanup.ConfigureAwait(false); }
        catch (Exception exception) { Logger.Error("API background cleanup failed: {ErrorType}", exception.GetType().Name); }
    }
}
