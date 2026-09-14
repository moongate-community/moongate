namespace Moongate.Tests.TestSupport.Buffers;

public sealed class FallbackFormattable : IFormattable
{
    public string? ReceivedFormat { get; private set; }

    public IFormatProvider? ReceivedProvider { get; private set; }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        ReceivedFormat = format;
        ReceivedProvider = formatProvider;

        return "formatted value";
    }
}
