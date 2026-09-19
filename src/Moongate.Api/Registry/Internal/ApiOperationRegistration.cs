using Moongate.Api.Data.Requests;
using Moongate.Api.Interfaces.Contracts;
using Moongate.Api.Interfaces.Handlers;
using Moongate.Api.Interfaces.Internal.Registry;
using Moongate.Api.Serialization.Internal;

namespace Moongate.Api.Registry.Internal;

internal sealed class ApiOperationRegistration<TRequest, TResponse> : IApiOperationRegistration
    where TRequest : IApiRequest<TResponse>
{
    private readonly Func<IApiHandler<TRequest, TResponse>>? _factory;
    private IApiHandler<TRequest, TResponse>? _handler;
    public ushort Id { get; }
    public Type RequestType => typeof(TRequest);
    public Type ResponseType => typeof(TResponse);
    public bool HasHandler => _factory is not null;

    public ApiOperationRegistration(ushort id, Func<IApiHandler<TRequest, TResponse>>? factory = null)
    {
        Id = id;
        _factory = factory;
    }

    public object DeserializeRequest(ReadOnlyMemory<byte> payload)
        => ApiPayloadSerializer.Deserialize<TRequest>(payload)!;

    public object DeserializeResponse(ReadOnlyMemory<byte> payload)
        => ApiPayloadSerializer.Deserialize<TResponse>(payload)!;

    public async ValueTask<byte[]> InvokeAsync(
        ApiRequestContext context,
        object request,
        int maxLength,
        CancellationToken token
    )
    {
        var handler = _handler ?? throw new InvalidOperationException("No resolved handler.");
        var response = await handler.HandleAsync(context, (TRequest)request, token).ConfigureAwait(false);

        return ApiPayloadSerializer.Serialize(response, maxLength);
    }

    public void ResolveHandler()
    {
        if (_factory is not null)
        {
            _handler = _factory() ?? throw new InvalidOperationException("Handler factory returned null.");
        }
    }

    public byte[] SerializeRequest(object request, int maxLength)
        => ApiPayloadSerializer.Serialize((TRequest)request, maxLength);
}
