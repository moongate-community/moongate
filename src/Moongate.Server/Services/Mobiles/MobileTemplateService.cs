using Moongate.Server.Interfaces.Mobiles;
using Moongate.UO.Data.Mobiles.Templates;

namespace Moongate.Server.Services.Mobiles;

/// <summary>Default <see cref="IMobileTemplateService" />: a dictionary of templates keyed by id.</summary>
public sealed class MobileTemplateService : IMobileTemplateService
{
    private readonly Dictionary<string, MobileTemplate> _byId = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<MobileTemplate> All
        => _byId.Values.ToList();

    public int Count
        => _byId.Count;

    public MobileTemplate? GetById(string id)
        => _byId.GetValueOrDefault(id);

    public IEnumerable<MobileTemplate> GetByTag(string tag)
        => _byId.Values.Where(template => template.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));

    public IEnumerable<MobileTemplate> GetByCategory(string category)
        => _byId.Values.Where(template => string.Equals(template.Category, category, StringComparison.OrdinalIgnoreCase));

    public void Register(MobileTemplate mobileTemplate)
        => _byId[mobileTemplate.Id] = mobileTemplate;
}
