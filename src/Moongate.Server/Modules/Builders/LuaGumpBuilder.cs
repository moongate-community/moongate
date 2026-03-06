using System.Text;

namespace Moongate.Server.Modules.Builders;

/// <summary>
/// Script-facing gump layout builder.
/// </summary>
public sealed class LuaGumpBuilder
{
    private readonly StringBuilder _layout = new();
    private readonly List<string> _texts = [];

    public LuaGumpBuilder AlphaRegion(int x, int y, int width, int height)
    {
        AppendToken($"{{ checkertrans {x} {y} {width} {height} }}");

        return this;
    }

    public string BuildLayout()
        => _layout.ToString();

    public List<string> BuildTexts()
        => [.._texts];

    public LuaGumpBuilder Button(int x, int y, int normalId, int pressedId, int buttonId)
    {
        AppendToken($"{{ button {x} {y} {normalId} {pressedId} 1 0 {buttonId} }}");

        return this;
    }

    public LuaGumpBuilder ButtonPage(int x, int y, int normalId, int pressedId, int pageId)
    {
        AppendToken($"{{ button {x} {y} {normalId} {pressedId} 0 {pageId} 0 }}");

        return this;
    }

    public LuaGumpBuilder CheckBox(int x, int y, int inactiveId, int activeId, int switchId, bool initialState = false)
    {
        AppendToken($"{{ checkbox {x} {y} {inactiveId} {activeId} {(initialState ? 1 : 0)} {switchId} }}");

        return this;
    }

    public LuaGumpBuilder Group(int groupId)
    {
        AppendToken($"{{ group {groupId} }}");

        return this;
    }

    public LuaGumpBuilder Html(
        int x,
        int y,
        int width,
        int height,
        string text,
        bool background = true,
        bool scrollbar = false
    )
        => HtmlLocalized(x, y, width, height, text, background, scrollbar);

    public LuaGumpBuilder HtmlLocalized(
        int x,
        int y,
        int width,
        int height,
        string text,
        bool background = true,
        bool scrollbar = false
    )
    {
        var index = _texts.Count;
        _texts.Add(text ?? string.Empty);
        AppendToken($"{{ htmlgump {x} {y} {width} {height} {index} {(background ? 1 : 0)} {(scrollbar ? 1 : 0)} }}");

        return this;
    }

    public LuaGumpBuilder Image(int x, int y, int gumpId, int hue = 0)
    {
        if (hue <= 0)
        {
            AppendToken($"{{ gumppic {x} {y} {gumpId} }}");
        }
        else
        {
            AppendToken($"{{ gumppic {x} {y} {gumpId} hue={hue} }}");
        }

        return this;
    }

    public LuaGumpBuilder ImageTiled(int x, int y, int width, int height, int gumpId)
    {
        AppendToken($"{{ gumppictiled {x} {y} {width} {height} {gumpId} }}");

        return this;
    }

    public LuaGumpBuilder Item(int x, int y, int itemId, int hue = 0)
    {
        if (hue <= 0)
        {
            AppendToken($"{{ tilepic {x} {y} {itemId} }}");
        }
        else
        {
            AppendToken($"{{ tilepichue {x} {y} {itemId} {hue} }}");
        }

        return this;
    }

    public LuaGumpBuilder LabelCropped(int x, int y, int width, int height, int hue, string text)
    {
        var index = _texts.Count;
        _texts.Add(text ?? string.Empty);
        AppendToken($"{{ croppedtext {x} {y} {width} {height} {hue} {index} }}");

        return this;
    }

    public LuaGumpBuilder NoClose()
    {
        AppendToken("{ noclose }");

        return this;
    }

    public LuaGumpBuilder NoMove()
    {
        AppendToken("{ nomove }");

        return this;
    }

    public LuaGumpBuilder Page(int pageId = 0)
    {
        AppendToken($"{{ page {pageId} }}");

        return this;
    }

    public LuaGumpBuilder Radio(int x, int y, int inactiveId, int activeId, int switchId, bool initialState = false)
    {
        AppendToken($"{{ radio {x} {y} {inactiveId} {activeId} {(initialState ? 1 : 0)} {switchId} }}");

        return this;
    }

    public LuaGumpBuilder ResizePic(int x, int y, int gumpId, int width, int height)
    {
        AppendToken($"{{ resizepic {x} {y} {gumpId} {width} {height} }}");

        return this;
    }

    public LuaGumpBuilder Text(int x, int y, int hue, string text)
    {
        var index = _texts.Count;
        _texts.Add(text ?? string.Empty);
        AppendToken($"{{ text {x} {y} {hue} {index} }}");

        return this;
    }

    public LuaGumpBuilder TextEntry(int x, int y, int width, int height, int hue, int entryId, string text)
    {
        var index = _texts.Count;
        _texts.Add(text ?? string.Empty);
        AppendToken($"{{ textentry {x} {y} {width} {height} {hue} {entryId} {index} }}");

        return this;
    }

    public LuaGumpBuilder TextEntryLimited(int x, int y, int width, int height, int hue, int entryId, string text, int size)
    {
        var index = _texts.Count;
        _texts.Add(text ?? string.Empty);
        AppendToken($"{{ textentrylimited {x} {y} {width} {height} {hue} {entryId} {index} {size} }}");

        return this;
    }

    public LuaGumpBuilder ToolTip(int number)
    {
        AppendToken($"{{ tooltip {number} }}");

        return this;
    }

    private void AppendToken(string token)
    {
        if (_layout.Length > 0)
        {
            _layout.Append(' ');
        }

        _layout.Append(token);
    }
}
