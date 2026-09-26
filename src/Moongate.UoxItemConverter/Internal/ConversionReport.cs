namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Counts what the mobile pass dropped or could not carry over, by reason, for the summary it prints.
/// </summary>
internal sealed class ConversionReport
{
    private readonly SortedDictionary<string, int> _counts = new(StringComparer.Ordinal);

    /// <summary>
    ///     Gets each reason with how often it happened.
    /// </summary>
    public IEnumerable<(string Reason, int Count)> Lines => _counts.Select(pair => (pair.Key, pair.Value));

    /// <summary>
    ///     Counts one occurrence of <paramref name="reason" />.
    /// </summary>
    public void Count(string reason)
    {
        _counts[reason] = _counts.GetValueOrDefault(reason) + 1;
    }
}
