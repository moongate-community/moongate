using System.Data.Common;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Moongate.Server.Core.Exceptions.Admin;
using Serilog;

namespace Moongate.Server.Admin.Internal;

internal sealed class AdminExceptionInterceptor : Interceptor
{
    private readonly ILogger _logger = Log.ForContext<AdminExceptionInterceptor>();

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation
    )
    {
        try
        {
            var response = await continuation(request, context);
            context.GetHttpContext().Items["AdminStatus"] = StatusCode.OK.ToString();

            return response;
        }
        catch (Exception exception)
        {
            var status = MapStatus(exception, context.CancellationToken);
            context.GetHttpContext().Items["AdminStatus"] = status.StatusCode.ToString();

            if (status.StatusCode == StatusCode.Internal)
            {
                // Provider exception bodies can contain SQL parameters or connection strings.
                _logger.Error("Administration operation failed with {ExceptionType}", exception.GetType().Name);
            }

            throw new RpcException(status);
        }
    }

    internal static Status MapStatus(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new(StatusCode.Cancelled, "Administration call canceled.");
        }

        return exception switch
        {
            RpcException rpc           => rpc.Status,
            OperationCanceledException => new(StatusCode.Cancelled, "Administration call canceled."),
            AdminSessionLimitException => new(StatusCode.ResourceExhausted, "Account session limit reached."),
            AdminDependencyUnavailableException or AdminSessionRejectedException => new(
                StatusCode.Unavailable,
                "Administration dependency unavailable."
            ),
            KeyNotFoundException => new(StatusCode.NotFound, "Account not found."),
            _ when IsDependencyUnavailable(exception) => new(
                StatusCode.Unavailable,
                "Administration dependency unavailable."
            ),
            _ => new(StatusCode.Internal, "Administration operation failed.")
        };
    }

    private static bool IsDependencyUnavailable(Exception exception)
    {
        // FreeSql can wrap provider failures; never classify by exception message text.
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException { IsTransient: true } or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }
}
