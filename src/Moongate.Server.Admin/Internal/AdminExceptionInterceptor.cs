using Grpc.Core;
using Grpc.Core.Interceptors;
using Moongate.Server.Core.Exceptions.Admin;
using Serilog;

namespace Moongate.Server.Admin.Internal;

internal sealed class AdminExceptionInterceptor : Interceptor
{
    private readonly ILogger _logger = Log.ForContext<AdminExceptionInterceptor>();

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request,
        ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            var response = await continuation(request, context);
            context.GetHttpContext().Items["AdminStatus"] = StatusCode.OK.ToString();
            return response;
        }
        catch (Exception exception)
        {
            var status = exception switch
            {
                RpcException rpc => rpc.Status,
                OperationCanceledException => new Status(StatusCode.Cancelled, "Administration call canceled."),
                AdminSessionLimitException => new Status(StatusCode.ResourceExhausted, "Account session limit reached."),
                AdminDependencyUnavailableException or AdminSessionRejectedException => new Status(StatusCode.Unavailable, "Administration dependency unavailable."),
                KeyNotFoundException => new Status(StatusCode.NotFound, "Account not found."),
                _ => new Status(StatusCode.Internal, "Administration operation failed.")
            };
            context.GetHttpContext().Items["AdminStatus"] = status.StatusCode.ToString();
            if (status.StatusCode == StatusCode.Internal)
            {
                // Provider exception bodies can contain SQL parameters or connection strings.
                _logger.Error("Administration operation failed with {ExceptionType}", exception.GetType().Name);
            }
            throw new RpcException(status);
        }
    }
}
