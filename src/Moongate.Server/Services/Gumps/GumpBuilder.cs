using System.Globalization;
using System.Text;
using Moongate.Server.Abstractions.Interfaces.Gumps;

namespace Moongate.Server.Services.Gumps;

/// <summary>
/// Accumulates the two things a gump is on the wire: the layout command string, and the block of
/// strings the layout indexes into. It also remembers which button, switch and text-entry ids it
/// emitted, which is what lets the service reject a response naming an id that was never drawn.
/// </summary>
public sealed class GumpBuilder : IGumpBuilder
{
    private readonly StringBuilder _layout = new();
    private readonly List<string> _strings = [];
    private readonly Dictionary<string, int> _stringIndex = new(StringComparer.Ordinal);
    private readonly HashSet<int> _buttonIds = [];
    private readonly HashSet<int> _switchIds = [];
    private readonly HashSet<int> _textEntryIds = [];

    public string Layout => _layout.ToString();

    public IReadOnlyList<string> Strings => _strings;

    public IReadOnlySet<int> ButtonIds => _buttonIds;

    public IReadOnlySet<int> SwitchIds => _switchIds;

    public IReadOnlySet<int> TextEntryIds => _textEntryIds;

    public void AddPage(int page)
    {
        _layout.Append(CultureInfo.InvariantCulture, $"{{ page {page} }}");
    }

    public void AddBackground(int x, int y, int width, int height, int gumpId)
    {
        _layout.Append(CultureInfo.InvariantCulture, $"{{ resizepic {x} {y} {gumpId} {width} {height} }}");
    }

    public void AddLabel(int x, int y, int hue, string text)
    {
        _layout.Append(CultureInfo.InvariantCulture, $"{{ text {x} {y} {hue} {Intern(text)} }}");
    }

    public void AddButton(int x, int y, int normalId, int pressedId, int buttonId, int type, int param)
    {
        _buttonIds.Add(buttonId);

        // The wire order is not the argument order: type and param precede the id.
        _layout.Append(
            CultureInfo.InvariantCulture,
            $"{{ button {x} {y} {normalId} {pressedId} {type} {param} {buttonId} }}"
        );
    }

    public void AddCheck(int x, int y, int inactiveId, int activeId, bool initialState, int switchId)
    {
        _switchIds.Add(switchId);

        _layout.Append(
            CultureInfo.InvariantCulture,
            $"{{ checkbox {x} {y} {inactiveId} {activeId} {(initialState ? 1 : 0)} {switchId} }}"
        );
    }

    public void AddTextEntry(int x, int y, int width, int height, int hue, int entryId, string initialText)
    {
        _textEntryIds.Add(entryId);

        _layout.Append(
            CultureInfo.InvariantCulture,
            $"{{ textentry {x} {y} {width} {height} {hue} {entryId} {Intern(initialText)} }}"
        );
    }

    /// <summary>
    /// Returns the index of <paramref name="text" /> in the strings block, adding it first if it is
    /// new. Repeated captions are common in a gump and the block is sent once for the whole gump,
    /// so an identical string is stored once.
    /// </summary>
    private int Intern(string text)
    {
        if (_stringIndex.TryGetValue(text, out var index))
        {
            return index;
        }

        index = _strings.Count;
        _strings.Add(text);
        _stringIndex[text] = index;

        return index;
    }
}
