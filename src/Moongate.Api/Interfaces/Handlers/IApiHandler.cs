using Moongate.Api.Data.Requests;
using Moongate.Api.Interfaces.Contracts;

namespace Moongate.Api.Interfaces.Handlers;

/// <summary>Handles a typed operation outside the network receive callback.</summary>
/// <typeparam name="TRequest">The request contract.</typeparam>
/// <typeparam name="TResponse">The response contract.</typeparam>
public interface IApiHandler<in TRequest, TResponse> where TRequest : IApiRequest<TResponse>
{
    /// <summary>Executes the operation. Observe cancellation; handlers may run concurrently for different peers.</summary>
    ValueTask<TResponse> HandleAsync(ApiRequestContext context, TRequest request, CancellationToken cancellationToken);
}
