using Moongate.Api.Data.Requests;
using Moongate.Api.Interfaces.Handlers;

namespace Moongate.Tests.TestSupport.Api;

public sealed class FailingHandler : IApiHandler<IncrementRequest, IncrementResponse>
{
    public FailingHandler()
    {
        throw new InvalidOperationException("Constructor failed.");
    }

    public ValueTask<IncrementResponse> HandleAsync(
        ApiRequestContext context,
        IncrementRequest request,
        CancellationToken cancellationToken
    )
        => throw new NotSupportedException();
}
