using Moongate.Api.Data.Requests;

namespace Moongate.Api.Interfaces.Internal.Registry;

/// <summary>Describes an explicitly registered operation and its typed codec delegates.</summary>
internal interface IApiOperationRegistration
{
    /// <summary>Gets the stable operation identifier.</summary>
    ushort Id { get; }

    /// <summary>Gets the registered request type.</summary>
    Type RequestType { get; }

    /// <summary>Gets the registered response type.</summary>
    Type ResponseType { get; }

    /// <summary>Gets whether an incoming handler exists.</summary>
    bool HasHandler { get; }

    /// <summary>Validates and decodes an authorized request.</summary>
    object DeserializeRequest(ReadOnlyMemory<byte> payload);

    /// <summary>Reads a typed response without dynamic wire type resolution.</summary>
    object DeserializeResponse(ReadOnlyMemory<byte> payload);

    /// <summary>Invokes the handler and encodes the response within its byte budget.</summary>
    ValueTask<byte[]> InvokeAsync(ApiRequestContext context, object request, int maxLength, CancellationToken token);

    /// <summary>Resolves the singleton before the registry is exposed to connections.</summary>
    void ResolveHandler();

    /// <summary>Serializes an outgoing typed request within its byte budget.</summary>
    byte[] SerializeRequest(object request, int maxLength);
}
