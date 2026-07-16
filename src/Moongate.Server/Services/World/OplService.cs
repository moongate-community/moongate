using Moongate.Core.Primitives;
using Moongate.Network.Data;
using Moongate.Persistence.Entities;
using Moongate.Server.Data.World;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.World;
using Moongate.UO.Data.Types;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Server.Services.World;

/// <summary>
/// Builds object property lists the way ModernUO does — hash-as-revision, per-serial cache — but at
/// the service level: entities stay plain persistence records. Reads the entity stores directly
/// (not <see cref="IItemService" />) so item mutations can invalidate without a dependency cycle.
/// Raw-text lines rotate over five "~1_NOTHING~"-style clilocs so multiple lines on one object
/// render distinctly.
/// </summary>
public sealed class OplService : IOplService
{
    private const int StackCliloc = 1050039;      // ~1_NUMBER~ ~2_ITEMNAME~
    private const int MobileNameCliloc = 1050045; // ~1_PREFIX~~2_NAME~~3_SUFFIX~
    private const int WeightSingular = 1072788;   // Weight: ~1_WEIGHT~ stone
    private const int WeightPlural = 1072789;     // Weight: ~1_WEIGHT~ stones

    private static readonly int[] _stringClilocs = [1042971, 1070722, 1114057, 1114778, 1114779];

    private readonly IEntityStore<ItemEntity, Serial> _items;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly IItemTemplateService _templates;
    private readonly Dictionary<Serial, OplSnapshot> _cache = [];

    public OplService(IPersistenceService persistence, IItemTemplateService templates)
    {
        _items = persistence.GetStore<ItemEntity, Serial>();
        _mobiles = persistence.GetStore<MobileEntity, Serial>();
        _templates = templates;
    }

    public OplSnapshot GetOrBuild(Serial serial)
    {
        if (_cache.TryGetValue(serial, out var cached))
        {
            return cached;
        }

        var entries = Build(serial);
        var snapshot = new OplSnapshot(Hash(entries), entries);
        _cache[serial] = snapshot;

        return snapshot;
    }

    public void Invalidate(Serial serial)
        => _cache.Remove(serial);

    private List<OplEntry> Build(Serial serial)
    {
        if (serial.IsMobile && _mobiles.GetById(serial) is { } mobile)
        {
            return BuildMobile(mobile);
        }

        if (serial.IsItem && _items.GetById(serial) is { } item)
        {
            return BuildItem(item);
        }

        return [];
    }

    private static List<OplEntry> BuildMobile(MobileEntity mobile)
    {
        // The 1050045 slots must never be empty strings; a single space is the wire convention.
        var name = string.IsNullOrEmpty(mobile.Name) ? " " : mobile.Name;

        return [new OplEntry(MobileNameCliloc, $" \t{name}\t ")];
    }

    private List<OplEntry> BuildItem(ItemEntity item)
    {
        var entries = new List<OplEntry>();
        var rotation = 0;
        var template = _templates.GetById(item.TemplateId);
        var name = FirstNonEmpty(item.Name, template?.Name, "item");

        if (item.Amount > 1)
        {
            entries.Add(new OplEntry(StackCliloc, $"{item.Amount}\t{name}"));
        }
        else
        {
            entries.Add(RawText(name, ref rotation));
        }

        if (template is { Weight: > 0 })
        {
            var weight = (int)template.Weight;
            entries.Add(new OplEntry(weight == 1 ? WeightSingular : WeightPlural, weight.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            entries.Add(RawText(item.Description, ref rotation));
        }

        if (item.Rarity != ItemRarityType.Common)
        {
            entries.Add(RawText(item.Rarity.ToString(), ref rotation));
        }

        return entries;
    }

    private static OplEntry RawText(string text, ref int rotation)
        => new(_stringClilocs[rotation++ % _stringClilocs.Length], text);

    private static string FirstNonEmpty(string? first, string? second, string fallback)
        => !string.IsNullOrWhiteSpace(first) ? first
            : !string.IsNullOrWhiteSpace(second) ? second
            : fallback;

    /// <summary>
    /// ModernUO's OPL hash: every value folded as <c>h ^= v &amp; 0x3FFFFFF; h ^= (v &gt;&gt; 26) &amp; 0x3F</c>.
    /// Strings go through an explicit FNV-1a first — string.GetHashCode() is randomized per process
    /// and would change every revision across server restarts.
    /// </summary>
    private static int Hash(List<OplEntry> entries)
    {
        var hash = 0;

        foreach (var entry in entries)
        {
            Fold(ref hash, entry.Cliloc);

            if (entry.Arguments.Length > 0)
            {
                Fold(ref hash, Fnv1a(entry.Arguments));
            }
        }

        return hash;
    }

    private static void Fold(ref int hash, int value)
    {
        hash ^= value & 0x3FFFFFF;
        hash ^= (value >> 26) & 0x3F;
    }

    private static int Fnv1a(string text)
    {
        var hash = unchecked((int)2166136261);

        foreach (var ch in text)
        {
            hash = unchecked((hash ^ ch) * 16777619);
        }

        return hash;
    }
}
