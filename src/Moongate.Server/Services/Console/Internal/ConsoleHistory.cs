namespace Moongate.Server.Services.Console.Internal;

/// <summary>
///     The lines submitted on the console in this run, walked with Up and Down as a shell does: the newest first, and
///     back to the line being typed past the newest.
/// </summary>
internal sealed class ConsoleHistory
{
    private const int Capacity = 100;

    private readonly List<string> _lines = [];

    // _lines.Count when no line is recalled.
    private int _position;
    private string _draft = "";

    /// <summary>
    ///     Keeps a submitted line, unless it is blank or the same as the newest, and starts the walk again from the newest.
    /// </summary>
    public void Add(string line)
    {
        if (!string.IsNullOrWhiteSpace(line) && (_lines.Count == 0 || _lines[^1] != line))
        {
            _lines.Add(line);

            if (_lines.Count > Capacity)
            {
                _lines.RemoveAt(0);
            }
        }

        Reset();
    }

    /// <summary>
    ///     Gets the line before the one recalled, or the newest; null for an empty history. Leaving the line being typed
    ///     keeps it as <paramref name="current" /> for <see cref="Next" />.
    /// </summary>
    public string? Previous(string current)
    {
        if (_lines.Count == 0)
        {
            return null;
        }

        if (_position == _lines.Count)
        {
            _draft = current;
        }

        if (_position > 0)
        {
            _position--;
        }

        return _lines[_position];
    }

    /// <summary>
    ///     Gets the line after the one recalled, or the line that was being typed past the newest; null when no line is
    ///     recalled.
    /// </summary>
    public string? Next()
    {
        if (_position >= _lines.Count)
        {
            return null;
        }

        _position++;

        return _position == _lines.Count ? _draft : _lines[_position];
    }

    /// <summary>
    ///     Forgets the recalled line and the one being typed.
    /// </summary>
    public void Reset()
    {
        _position = _lines.Count;
        _draft = "";
    }
}
