using Moongate.Server.Interfaces.Items;
using Moongate.UO.Data.Loot;

namespace Moongate.Server.Services.Items;

public sealed class LootTemplateService : ILootTemplateService
{
    private readonly Dictionary<string, LootTemplate> _byId = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<LootTemplate> All
        => [.. _byId.Values.OrderBy(template => template.Id, StringComparer.OrdinalIgnoreCase)];

    public int Count => _byId.Count;

    public LootTemplate? GetById(string id)
        => _byId.GetValueOrDefault(id);

    public void Register(LootTemplate lootTemplate)
        => _byId[lootTemplate.Id] = lootTemplate;
}
