using Moongate.Server.Interfaces;
using Moongate.UO.Data.Loot;

namespace Moongate.Server.Services;

public sealed class LootTemplateService : ILootTemplateService
{
    private readonly Dictionary<string, LootTemplate> _byId = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<LootTemplate> All
        => [.. _byId.Values.OrderBy(template => template.Id, StringComparer.OrdinalIgnoreCase)];

    public int Count => _byId.Count;

    public void Register(LootTemplate lootTemplate)
    {
        _byId[lootTemplate.Id] = lootTemplate;
    }

    public LootTemplate? GetById(string id)
    {
        return _byId.GetValueOrDefault(id);
    }
}
