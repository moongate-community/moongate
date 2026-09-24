namespace Moongate.Tests.TestSupport.Buffers;

public sealed class InvalidSpanFormattable : ISpanFormattable
{
    private readonly bool _negativeCount;

    public InvalidSpanFormattable(bool negativeCount)
    {
        _negativeCount = negativeCount;
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
        => throw new InvalidOperationException("The builder must use span formatting.");

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        charsWritten = _negativeCount ? -1 : destination.Length + 1;

        return true;
    }
}
