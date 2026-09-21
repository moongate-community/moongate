using System.Threading.Channels;
using Moongate.Api.Data.Requests;
using Moongate.Api.Interfaces.Handlers;
using Moongate.Api.Tests.TestSupport.Contracts;

namespace Moongate.Api.Tests.TestSupport.Handlers;

internal sealed class GatedHandler : IApiHandler<IncrementRequest, IncrementResponse>
{
    private readonly bool _cooperative;
    private readonly bool _throwAfterRelease;
    private int _count;
    public Channel<int> Entered { get; } = Channel.CreateUnbounded<int>();
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Count => Volatile.Read(ref _count);

    public GatedHandler(bool cooperative = true, bool throwAfterRelease = false)
    {
        _cooperative = cooperative;
        _throwAfterRelease = throwAfterRelease;
    }

    public async ValueTask<IncrementResponse> HandleAsync(
        ApiRequestContext context,
        IncrementRequest request,
        CancellationToken cancellationToken
    )
    {
        Entered.Writer.TryWrite(Interlocked.Increment(ref _count));

        if (_cooperative)
        {
            await Release.Task.WaitAsync(cancellationToken);
        }
        else
        {
            await Release.Task;
        }

        if (_throwAfterRelease)
        {
            throw new InvalidOperationException("Private handler details must not reach the wire.");
        }

        return new() { Value = request.Value + 1 };
    }
}
