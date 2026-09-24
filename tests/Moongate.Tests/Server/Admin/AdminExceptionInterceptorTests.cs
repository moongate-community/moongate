using Grpc.Core;
using Moongate.Server.Admin.Internal;

namespace Moongate.Tests.Server.Admin;

public sealed class AdminExceptionInterceptorTests
{
    [Fact]
    public void MapStatus_CanceledCallWithWrappedProviderFailure_ReturnsCanceled()
    {
        var exception = new InvalidOperationException("provider wrapper", new OperationCanceledException());
        var status = AdminExceptionInterceptor.MapStatus(exception, new(true));
        Assert.Equal(StatusCode.Cancelled, status.StatusCode);
        Assert.DoesNotContain("provider", status.Detail);
    }
}
