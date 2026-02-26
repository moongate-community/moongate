using System.Text;
using Moongate.Network.Packets.Outgoing.UI;

namespace Moongate.Network.Packets.Helpers;

/// <summary>
/// Fluent builder for UO generic gump layout strings and text lines.
/// </summary>
public sealed class GumpBuilder
{
    private readonly StringBuilder _layout = new();
    private readonly List<string> _texts = [];

    /// <summary>
    /// Builds final layout string.
    /// </summary>
    public string BuildLayout()
        => _layout.ToString();

    /// <summary>
    /// Builds final string table.
    /// </summary>
    public IReadOnlyList<string> BuildTexts()
        => _texts.AsReadOnly();

    /// <summary>
    /// Adds a reply button. Pressing it closes the gump and sends 0xB1 with the selected button id.
    /// </summary>
    public GumpBuilder Button(int x, int y, int normalId, int pressedId, int buttonId)
    {
        AppendToken($"{{ button {x} {y} {normalId} {pressedId} 1 0 {buttonId} }}");

        return this;
    }

    /// <summary>
    /// Adds a page button that switches page client-side without a server round trip.
    /// </summary>
    public GumpBuilder ButtonPage(int x, int y, int normalId, int pressedId, int pageId)
    {
        AppendToken($"{{ button {x} {y} {normalId} {pressedId} 0 {pageId} 0 }}");

        return this;
    }

    /// <summary>
    /// Adds a checkbox and binds it to a switch id for response handling.
    /// </summary>
    public GumpBuilder CheckBox(int x, int y, int inactiveId, int activeId, int switchId, bool initialState = false)
    {
        AppendToken($"{{ checkbox {x} {y} {inactiveId} {activeId} {(initialState ? 1 : 0)} {switchId} }}");

        return this;
    }

    /// <summary>
    /// Adds a HTML text area and stores the text in the gump string table.
    /// </summary>
    public GumpBuilder HtmlLocalized(
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

    /// <summary>
    /// Sets gump as non-closable.
    /// </summary>
    public GumpBuilder NoClose()
    {
        AppendToken("{ noclose }");

        return this;
    }

    /// <summary>
    /// Sets gump as non-movable.
    /// </summary>
    public GumpBuilder NoMove()
    {
        AppendToken("{ nomove }");

        return this;
    }

    /// <summary>
    /// Adds a resizable background tile at the given position.
    /// </summary>
    public GumpBuilder ResizePic(int x, int y, int gumpId, int width, int height)
    {
        AppendToken($"{{ resizepic {x} {y} {gumpId} {width} {height} }}");

        return this;
    }

    /// <summary>
    /// Adds a static text label using a pre-registered text line.
    /// </summary>
    public GumpBuilder Text(int x, int y, int hue, string text)
    {
        var index = _texts.Count;
        _texts.Add(text ?? string.Empty);
        AppendToken($"{{ text {x} {y} {hue} {index} }}");

        return this;
    }

    /// <summary>
    /// Builds a compressed gump packet (0xDD) from the current builder state.
    /// </summary>
    public CompressedGumpPacket ToCompressedPacket(uint senderSerial, uint gumpId, uint x, uint y)
    {
        var packet = new CompressedGumpPacket
        {
            SenderSerial = senderSerial,
            GumpId = gumpId,
            X = x,
            Y = y,
            Layout = BuildLayout()
        };
        packet.TextLines.AddRange(_texts);

        return packet;
    }

    /// <summary>
    /// Builds an uncompressed generic gump packet (0xB0) from the current builder state.
    /// </summary>
    public GenericGumpPacket ToGenericPacket(uint senderSerial, uint gumpId, uint x, uint y)
    {
        var packet = new GenericGumpPacket
        {
            SenderSerial = senderSerial,
            GumpId = gumpId,
            X = x,
            Y = y,
            Layout = BuildLayout()
        };
        packet.TextLines.AddRange(_texts);

        return packet;
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
