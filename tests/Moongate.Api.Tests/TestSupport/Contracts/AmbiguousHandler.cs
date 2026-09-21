using Moongate.Api.Data.Requests;
using Moongate.Api.Interfaces.Handlers;

namespace Moongate.Api.Tests.TestSupport.Contracts;

public sealed class AmbiguousHandler
    : IApiHandler<IncrementRequest, IncrementResponse>, IApiHandler<AlternativeRequest, IncrementResponse>
{
    public ValueTask<IncrementResponse> HandleAsync(
        ApiRequestContext context, IncrementRequest request, CancellationToken cancellationToken
    )
        => ValueTask.FromResult(new IncrementResponse());

    public ValueTask<IncrementResponse> HandleAsync(
        ApiRequestContext context, AlternativeRequest request, CancellationToken cancellationToken
    )
        => ValueTask.FromResult(new IncrementResponse());
}
