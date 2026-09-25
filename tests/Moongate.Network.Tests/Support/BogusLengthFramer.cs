using Moongate.Network.Interfaces.Framing;

namespace Moongate.Network.Tests.Support;

/// <summary>
///     Reports a configured length as soon as any byte is available.
/// </summary>
public sealed class BogusLengthFramer : INetFramer
{
    private readonly int _reportedLength;

    public BogusLengthFramer(int reportedLength)
    {
        _reportedLength = reportedLength;
    }

    public bool TryReadFrame(Span<byte> buffer, out int frameLength)
    {
        frameLength = _reportedLength;

        return !buffer.IsEmpty;
    }
}
