using Moongate.Api.Data.Requests;
using Moongate.Api.Interfaces.Handlers;
using Moongate.Api.TestHost.Data;

namespace Moongate.Api.TestHost.Handlers;

internal sealed class IncrementHandler : IApiHandler<IncrementRequest, IncrementResponse>
{
    private int _invocations;
    public int Invocations => Volatile.Read(ref _invocations);

    public async ValueTask<IncrementResponse> HandleAsync(
        ApiRequestContext context, IncrementRequest request, CancellationToken cancellationToken
    )
    {
        Interlocked.Increment(ref _invocations);
        if (request.Value == -1)
        {
            await context.Connection.CloseAsync(cancellationToken);
        }

        return new IncrementResponse { Value = request.Value + 1 };
    }
}
