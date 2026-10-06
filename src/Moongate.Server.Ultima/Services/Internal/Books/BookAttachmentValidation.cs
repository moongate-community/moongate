using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services.Internal.Books;

internal static class BookAttachmentValidation
{
    public const int MaximumItems = 32;
    public const int MaximumAmount = ushort.MaxValue;

    public static void Validate(BookTemplate book, IReadOnlyDictionary<string, ItemTemplate> items, ITileDataService? tiles)
    {
        var count = 0L;
        foreach (var entry in book.Attachments)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.ItemTemplate) ||
                !items.TryGetValue(entry.ItemTemplate, out var item))
            {
                throw new InvalidDataException($"{book.File}: unknown attachment item_template.");
            }

            var min = entry.Amount?.Min ?? 1;
            var max = entry.Amount?.Max ?? 1;
            if (min < 1 || max > MaximumAmount || max < min)
            {
                throw new InvalidDataException($"{book.File}: attachment amount must stay within 1..65535.");
            }

            var stacks = item.Stackable ?? (tiles is not null
                ? item.EffectiveStackable(tiles)
                : throw new InvalidDataException($"{book.File}: tiledata is required to validate attachment stackability."));
            count += stacks ? 1 : max;
            if (count > MaximumItems)
            {
                throw new InvalidDataException($"{book.File}: attachments exceed 32 physical items.");
            }
        }
    }
}
