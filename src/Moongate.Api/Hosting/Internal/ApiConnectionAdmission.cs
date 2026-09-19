namespace Moongate.Api.Hosting.Internal;

/// <summary>Transfers one endpoint reservation from stream setup to the complete API lifetime.</summary>
internal sealed class ApiConnectionAdmission : IDisposable
{
    private const int SetupOwner = 0;
    private const int ConnectionOwner = 1;
    private const int Released = 2;
    private readonly Action _release;
    private int _owner;

    public ApiConnectionAdmission(Action release)
    {
        _release = release;
    }

    public void TransferToConnection()
    {
        if (Interlocked.CompareExchange(ref _owner, ConnectionOwner, SetupOwner) != SetupOwner)
        {
            throw new IOException("The API setup reservation is no longer available.");
        }
    }

    public void ReleaseTransport()
    {
        if (Interlocked.CompareExchange(ref _owner, Released, SetupOwner) == SetupOwner) { _release(); }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _owner, Released) != Released) { _release(); }
    }
}
