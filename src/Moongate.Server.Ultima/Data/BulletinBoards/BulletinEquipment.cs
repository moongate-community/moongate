using System.Globalization;

namespace Moongate.Server.Ultima.Data.BulletinBoards;

/// <summary>
///     One piece of what the poster of a bulletin board message wore: the client draws the poster with it.
/// </summary>
public readonly record struct BulletinEquipment
{
    public int ItemId { get; }

    public int Hue { get; }

    public BulletinEquipment(int itemId, int hue)
    {
        ItemId = itemId;
        Hue = hue;
    }

    /// <summary>
    ///     Gets the pieces as the text a message keeps them in: itemId:hue pairs joined with commas.
    /// </summary>
    public static string Format(IEnumerable<BulletinEquipment> pieces)
    {
        return string.Join(
            ',',
            pieces.Select(piece => string.Create(CultureInfo.InvariantCulture, $"{piece.ItemId}:{piece.Hue}"))
        );
    }

    /// <summary>
    ///     Gets the pieces back from that text; what is not a pair of numbers is left out.
    /// </summary>
    public static IReadOnlyList<BulletinEquipment> Parse(string? text)
    {
        var pieces = new List<BulletinEquipment>();

        foreach (var pair in (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split(':');

            if (parts.Length == 2 &&
                int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var itemId) &&
                int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var hue))
            {
                pieces.Add(new(itemId, hue));
            }
        }

        return pieces;
    }
}
