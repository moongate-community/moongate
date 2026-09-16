namespace Moongate.Tests.Support.Timing;

/// <summary>Allows a callback to break subsequent diagnostic timestamp reads.</summary>
public sealed class FaultingTimeProvider : TimeProvider
{
    private readonly ManualTimeProvider _clock;

    public bool FailTimestampReads { get; set; }
    public override long TimestampFrequency => _clock.TimestampFrequency;

    public FaultingTimeProvider(ManualTimeProvider clock)
    {
        _clock = clock;
    }

    public override long GetTimestamp()
    {
        if (FailTimestampReads)
        {
            throw new InvalidOperationException("Diagnostic clock failed.");
        }

        return _clock.GetTimestamp();
    }
}
