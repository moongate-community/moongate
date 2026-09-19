using Moongate.Api.Interfaces.Internal.Registry;
namespace Moongate.Api.Data.Internal.Requests;
internal sealed class ApiPendingCall
{
    public IApiOperationRegistration Operation { get; }
    public TaskCompletionSource<object> Source { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public uint RequestId { get; set; }
    public ITimer? Deadline { get; set; }
    public CancellationTokenRegistration Cancellation { get; set; }
    public ApiPendingCall(IApiOperationRegistration operation) { Operation = operation; }
    public void Release()
    {
        Deadline?.Dispose();
        Cancellation.Unregister();
    }
}
