using Moongate.Api.Data.Requests;
using Moongate.Api.Interfaces.Handlers;
namespace Moongate.Api.Tests.TestSupport.Contracts;
public sealed class IncrementHandler : IApiHandler<IncrementRequest, IncrementResponse>
{
    public ValueTask<IncrementResponse> HandleAsync(ApiRequestContext context, IncrementRequest request, CancellationToken cancellationToken)
        => ValueTask.FromResult(new IncrementResponse { Value = request.Value + 1 });
}
