using System.Text;

namespace Moongate.Server.Modules.Builders;

/// <summary>
/// Script-facing gump layout builder.
/// </summary>
public sealed class LuaGumpBuilder
{
    private readonly StringBuilder _layout = new();
    private readonly List<string> _texts = [];

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
        AppendToken(
            $"{{ htmlgump {x} {y} {width} {height} {index} {(background ? 1 : 0)} {(scrollbar ? 1 : 0)} }}"
        );

        return this;
    }

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

    public string BuildLayout()
        => _layout.ToString();

    public List<string> BuildTexts()
        => [.._texts];

    private void AppendToken(string token)
    {
        if (_layout.Length > 0)
        {
            _layout.Append(' ');
        }

        _layout.Append(token);
    }
}
