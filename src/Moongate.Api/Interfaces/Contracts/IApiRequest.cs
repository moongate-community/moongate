namespace Moongate.Api.Interfaces.Contracts;

/// <summary>Identifies the response contract associated with a typed API request.</summary>
/// <typeparam name="TResponse">The serializable response type.</typeparam>
public interface IApiRequest<TResponse> { }
