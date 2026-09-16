using System.Buffers;

namespace Moongate.Network.Tests.Support;

public sealed class TrackingArrayPool : ArrayPool<byte>
{
    private readonly HashSet<byte[]> _outstanding;
    private readonly Lock _sync = new();

    public int LargestRequestedLength { get; private set; }
    public int OutstandingCount
    {
        get
        {
            lock (_sync)
            {
                return _outstanding.Count;
            }
        }
    }
    public int RentCount { get; private set; }
    public int ReturnCount { get; private set; }

    public TrackingArrayPool()
    {
        _outstanding = new HashSet<byte[]>(ReferenceEqualityComparer.Instance);
    }

    public override byte[] Rent(int minimumLength)
    {
        if (minimumLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumLength));
        }

        var buffer = new byte[minimumLength];

        lock (_sync)
        {
            RentCount++;
            LargestRequestedLength = Math.Max(LargestRequestedLength, minimumLength);
            _outstanding.Add(buffer);
        }

        return buffer;
    }

    public override void Return(byte[] array, bool clearArray = false)
    {
        ArgumentNullException.ThrowIfNull(array);

        lock (_sync)
        {
            if (!_outstanding.Remove(array))
            {
                throw new InvalidOperationException("The array was not rented or was already returned.");
            }

            ReturnCount++;
        }

        Array.Fill(array, (byte)0xDD);
    }
}
