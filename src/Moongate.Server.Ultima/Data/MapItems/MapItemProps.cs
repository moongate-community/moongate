using System.Globalization;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Data.MapItems;

/// <summary>
///     The props a map item keeps: the area it shows (<c>map.x1</c> to <c>map.facet</c>), its course of pins in pixels
///     of the drawing (<c>map.pins</c>, <c>x,y;x,y</c>), and whether the course may be changed (<c>map.editable</c>) or
///     never by a player (<c>map.protected</c>). A preset map without them takes its area from its template's tags
///     (<c>map_x1</c> to <c>map_facet</c>).
/// </summary>
public static class MapItemProps
{
    public const int MaxPins = 50;
    public const int MaxWorldX = 5119;
    public const int MaxWorldY = 4095;
    public const int MaxDrawing = 800;

    public const string X1Prop = "map.x1";
    public const string Y1Prop = "map.y1";
    public const string X2Prop = "map.x2";
    public const string Y2Prop = "map.y2";
    public const string WidthProp = "map.width";
    public const string HeightProp = "map.height";
    public const string FacetProp = "map.facet";
    public const string PinsProp = "map.pins";
    public const string EditableProp = "map.editable";
    public const string ProtectedProp = "map.protected";

    public static bool TryGetArea(ItemEntity item, ItemTemplate? template, out MapArea area)
    {
        if (TryGetNumber(item, X1Prop, out var x1) &&
            TryGetNumber(item, Y1Prop, out var y1) &&
            TryGetNumber(item, X2Prop, out var x2) &&
            TryGetNumber(item, Y2Prop, out var y2) &&
            TryGetNumber(item, WidthProp, out var width) &&
            TryGetNumber(item, HeightProp, out var height))
        {
            // A prop set by hand is kept on the world and the drawing the packets can carry.
            area = Clamp(new(x1, y1, x2, y2, width, height, TryGetNumber(item, FacetProp, out var facet) ? facet : 0));

            return true;
        }

        var tags = template?.Tags;

        if (tags is not null &&
            TryGetTag(tags, "map_x1", out x1) &&
            TryGetTag(tags, "map_y1", out y1) &&
            TryGetTag(tags, "map_x2", out x2) &&
            TryGetTag(tags, "map_y2", out y2) &&
            TryGetTag(tags, "map_width", out width) &&
            TryGetTag(tags, "map_height", out height))
        {
            area = Clamp(new(x1, y1, x2, y2, width, height, TryGetTag(tags, "map_facet", out var facet) ? facet : 0));

            return true;
        }

        // Safe: only read when the method returns true.
        area = null!;

        return false;
    }

    public static void SetArea(ItemEntity item, MapArea area)
    {
        area = Clamp(area);
        item.SetProp(X1Prop, (long)area.X1);
        item.SetProp(Y1Prop, (long)area.Y1);
        item.SetProp(X2Prop, (long)area.X2);
        item.SetProp(Y2Prop, (long)area.Y2);
        item.SetProp(WidthProp, (long)area.Width);
        item.SetProp(HeightProp, (long)area.Height);
        item.SetProp(FacetProp, (long)area.Facet);
    }

    /// <summary>
    ///     The area kept within the world (0 to 5119, 0 to 4095) and a drawing of 1 to 800 pixels.
    /// </summary>
    public static MapArea Clamp(MapArea area)
    {
        return new(
            Math.Clamp(area.X1, 0, MaxWorldX),
            Math.Clamp(area.Y1, 0, MaxWorldY),
            Math.Clamp(area.X2, 0, MaxWorldX),
            Math.Clamp(area.Y2, 0, MaxWorldY),
            Math.Clamp(area.Width, 1, MaxDrawing),
            Math.Clamp(area.Height, 1, MaxDrawing),
            area.Facet
        );
    }

    public static IReadOnlyList<(int X, int Y)> GetPins(ItemEntity item)
    {
        if (!item.TryGetProp<string>(PinsProp, out var text) || string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var pins = new List<(int X, int Y)>();

        foreach (var pair in text.Split(';'))
        {
            var parts = pair.Split(',');

            // A course written by hand that does not read is no course at all.
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
            {
                return [];
            }

            pins.Add((x, y));
        }

        return pins;
    }

    public static void SetPins(ItemEntity item, IReadOnlyList<(int X, int Y)> pins)
    {
        if (pins.Count == 0)
        {
            item.RemoveProp(PinsProp);

            return;
        }

        item.SetProp(
            PinsProp,
            string.Join(';', pins.Select(pin => string.Create(CultureInfo.InvariantCulture, $"{pin.X},{pin.Y}")))
        );
    }

    public static bool IsEditable(ItemEntity item)
    {
        return TryGetFlag(item, EditableProp);
    }

    public static void SetEditable(ItemEntity item, bool editable)
    {
        item.SetProp(EditableProp, editable);
    }

    public static bool IsProtected(ItemEntity item)
    {
        return TryGetFlag(item, ProtectedProp);
    }

    public static void SetProtected(ItemEntity item, bool guarded)
    {
        item.SetProp(ProtectedProp, guarded);
    }

    public static (int X, int Y) WorldToPixel(MapArea area, int x, int y)
    {
        var width = Math.Max(1, area.X2 - area.X1);
        var height = Math.Max(1, area.Y2 - area.Y1);

        // The far edge is the last pixel of the drawing, where the client can still move the pin.
        return (
            Math.Clamp((x - area.X1) * area.Width / width, 0, area.Width - 1),
            Math.Clamp((y - area.Y1) * area.Height / height, 0, area.Height - 1)
        );
    }

    private static bool TryGetNumber(ItemEntity item, string key, out int value)
    {
        value = 0;

        try
        {
            if (!item.TryGetProp<long>(key, out var stored))
            {
                return false;
            }

            value = (int)stored;

            return true;
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            return false;
        }
    }

    // A flag set by hand to text that is no bool reads as false.
    private static bool TryGetFlag(ItemEntity item, string key)
    {
        try
        {
            return item.TryGetProp<bool>(key, out var flag) && flag;
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException)
        {
            return false;
        }
    }

    private static bool TryGetTag(Dictionary<string, string> tags, string key, out int value)
    {
        value = 0;

        return tags.TryGetValue(key, out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
