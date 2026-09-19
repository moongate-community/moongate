using Moongate.Api.Types.Protocol;
namespace Moongate.Api.Exceptions;
/// <summary>The remote endpoint returned a protocol error for this operation.</summary>
public sealed class ApiRemoteException : Exception
{
    public ApiErrorCode Code { get; }
    public ApiRemoteException(ApiErrorCode code, string message) : base(message) { Code = code; }
}
