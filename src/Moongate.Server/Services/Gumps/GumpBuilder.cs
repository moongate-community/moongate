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

    public void AddAlphaRegion(int x, int y, int width, int height)
        => _layout.Append(CultureInfo.InvariantCulture, $"{{ checkertrans {x} {y} {width} {height} }}");

    public void AddBackground(int x, int y, int width, int height, int gumpId)
        => _layout.Append(CultureInfo.InvariantCulture, $"{{ resizepic {x} {y} {gumpId} {width} {height} }}");

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

    public void AddGroup(int group)
        => _layout.Append(CultureInfo.InvariantCulture, $"{{ group {group} }}");

    public void AddGumpIdOverride(int gumpId)
        => _layout.Append(CultureInfo.InvariantCulture, $"{{ mastergump {gumpId} }}");

    public void AddHtml(int x, int y, int width, int height, string text, bool background, bool scrollbar)
        => _layout.Append(
            CultureInfo.InvariantCulture,
            $"{{ htmlgump {x} {y} {width} {height} {Intern(text)} {Flag(background)} {Flag(scrollbar)} }}"
        );

    public void AddHtmlLocalized(
        int x,
        int y,
        int width,
        int height,
        int number,
        string? args,
        int? color,
        bool background,
        bool scrollbar
    )
    {
        // Three commands, chosen by what was supplied. Note that the argument form puts the flags
        // and the colour before the cliloc, unlike the other two.
        if (!string.IsNullOrEmpty(args))
        {
            _layout.Append(
                CultureInfo.InvariantCulture,
                $"{{ xmfhtmltok {x} {y} {width} {height} {Flag(background)} {Flag(scrollbar)} {color ?? 0} {number} @{args}@ }}"
            );

            return;
        }

        if (color is not null)
        {
            _layout.Append(
                CultureInfo.InvariantCulture,
                $"{{ xmfhtmlgumpcolor {x} {y} {width} {height} {number} {Flag(background)} {Flag(scrollbar)} {color} }}"
            );

            return;
        }

        _layout.Append(
            CultureInfo.InvariantCulture,
            $"{{ xmfhtmlgump {x} {y} {width} {height} {number} {Flag(background)} {Flag(scrollbar)} }}"
        );
    }

    public void AddImage(int x, int y, int gumpId, int hue)
    {
        if (hue == 0)
        {
            _layout.Append(CultureInfo.InvariantCulture, $"{{ gumppic {x} {y} {gumpId} }}");

            return;
        }

        _layout.Append(CultureInfo.InvariantCulture, $"{{ gumppic {x} {y} {gumpId} hue={hue} }}");
    }

    public void AddImageTiled(int x, int y, int width, int height, int gumpId)
        => _layout.Append(CultureInfo.InvariantCulture, $"{{ gumppictiled {x} {y} {width} {height} {gumpId} }}");

    public void AddImageTiledButton(
        int x,
        int y,
        int normalId,
        int pressedId,
        int buttonId,
        int type,
        int param,
        int itemId,
        int hue,
        int width,
        int height
    )
    {
        _buttonIds.Add(buttonId);

        _layout.Append(
            CultureInfo.InvariantCulture,
            $"{{ buttontileart {x} {y} {normalId} {pressedId} {type} {param} {buttonId} {itemId} {hue} {width} {height} }}"
        );
    }

    public void AddItem(int x, int y, int itemId, int hue)
    {
        // A hued item is a different command, not the same one with an extra argument.
        if (hue == 0)
        {
            _layout.Append(CultureInfo.InvariantCulture, $"{{ tilepic {x} {y} {itemId} }}");

            return;
        }

        _layout.Append(CultureInfo.InvariantCulture, $"{{ tilepichue {x} {y} {itemId} {hue} }}");
    }

    public void AddItemProperty(uint serial)
        => _layout.Append(CultureInfo.InvariantCulture, $"{{ itemproperty {serial} }}");

    public void AddLabel(int x, int y, int hue, string text)
        => _layout.Append(CultureInfo.InvariantCulture, $"{{ text {x} {y} {hue} {Intern(text)} }}");

    public void AddLabelCropped(int x, int y, int width, int height, int hue, string text)
        => _layout.Append(
            CultureInfo.InvariantCulture,
            $"{{ croppedtext {x} {y} {width} {height} {hue} {Intern(text)} }}"
        );

    public void AddLabelHtml(int x, int y, int width, int height, string text, string hue, int size, bool center)
    {
        // The command carries no styling, so colour, size and centring travel as markup on the text.
        var styled = $"<basefont color={hue} size={size}>{text}</basefont>";
        var markup = center ? $"<center>{styled}</center>" : styled;

        _layout.Append(
            CultureInfo.InvariantCulture,
            $"{{ htmlgump {x} {y} {width} {height} {Intern(markup)} 0 0 }}"
        );
    }

    public void AddPage(int page)
        => _layout.Append(CultureInfo.InvariantCulture, $"{{ page {page} }}");

    public void AddRadio(int x, int y, int inactiveId, int activeId, bool initialState, int switchId)
    {
        _switchIds.Add(switchId);

        _layout.Append(
            CultureInfo.InvariantCulture,
            $"{{ radio {x} {y} {inactiveId} {activeId} {(initialState ? 1 : 0)} {switchId} }}"
        );
    }

    public void AddSpriteImage(int x, int y, int gumpId, int width, int height, int sx, int sy)
        => _layout.Append(
            CultureInfo.InvariantCulture,
            $"{{ picinpic {x} {y} {gumpId} {width} {height} {sx} {sy} }}"
        );

    public void AddTextEntry(int x, int y, int width, int height, int hue, int entryId, string initialText)
    {
        _textEntryIds.Add(entryId);

        _layout.Append(
            CultureInfo.InvariantCulture,
            $"{{ textentry {x} {y} {width} {height} {hue} {entryId} {Intern(initialText)} }}"
        );
    }

    public void AddTooltip(int number, string? args)
    {
        if (string.IsNullOrEmpty(args))
        {
            _layout.Append(CultureInfo.InvariantCulture, $"{{ tooltip {number} }}");

            return;
        }

        _layout.Append(CultureInfo.InvariantCulture, $"{{ tooltip {number} @{args}@ }}");
    }

    private static int Flag(bool value)
        => value ? 1 : 0;

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
