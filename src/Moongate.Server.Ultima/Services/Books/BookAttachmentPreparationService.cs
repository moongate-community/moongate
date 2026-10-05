using System.Collections.Immutable;
using Moongate.Server.Ultima.Data.Internal.Books;
using Moongate.Server.Ultima.Data.Items;
using Moongate.Server.Ultima.Data.Templates.Books;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Books;
using Moongate.Server.Ultima.Services.Internal.Books;
using Moongate.Server.Ultima.Types.Templates;

namespace Moongate.Server.Ultima.Services.Books;

/// <summary>
///     Freezes an independent serial-free reward batch for each newly issued letter.
/// </summary>
public sealed class BookAttachmentPreparationService : IBookAttachmentPreparationService
{
    private readonly IItemFactoryService _factory;
    private readonly IItemTemplateService _templates;
    private readonly ITileDataService _tiles;

    public BookAttachmentPreparationService(IItemFactoryService factory, IItemTemplateService templates, ITileDataService tiles)
    {
        _factory = factory;
        _templates = templates;
        _tiles = tiles;
    }

    public string? Prepare(BookTemplateSource source)
    {
        if (source.Attachments.Count == 0)
        {
            return null;
        }
        var snapshots = ImmutableArray.CreateBuilder<FrozenBookAttachment>();
        foreach (var entry in source.Attachments)
        {
            if (!_templates.TryGet(entry.ItemTemplate, out var template))
            {
                throw new InvalidDataException($"Unknown attachment item template '{entry.ItemTemplate}'.");
            }
            var min = entry.Amount?.Min ?? 1;
            var max = entry.Amount?.Max ?? 1;
            var stacks = template.EffectiveStackable(_tiles);
            if (min < 1 || max < min || max > BookAttachmentValidation.MaximumAmount ||
                snapshots.Count + (long)(stacks ? 1 : max) > BookAttachmentValidation.MaximumItems)
            {
                throw new InvalidDataException("Invalid attachment amount or physical item count.");
            }
            var amount = entry.Amount?.Roll() ?? 1;
            for (var index = 0; index < (stacks ? 1 : amount); index++)
            {
                var item = _factory.Create(entry.ItemTemplate, stacks ? amount : 1, entry.Hue?.Resolve());
                var lootType = entry.Newbie ? LootType.Newbied : LootType.Regular;
                if (lootType != template.EffectiveLootType())
                {
                    item.SetProp(ItemPropKeys.LootType, lootType);
                }
                snapshots.Add(BookAttachmentCodec.Freeze(item));
            }
        }
        return BookAttachmentCodec.Encode(new() { Items = snapshots.ToImmutable() });
    }
}
