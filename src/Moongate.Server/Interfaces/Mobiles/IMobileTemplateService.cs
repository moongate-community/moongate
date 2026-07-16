using Moongate.UO.Data.Mobiles.Templates;

namespace Moongate.Server.Interfaces.Mobiles;

/// <summary>In-memory registry of mobile spawn templates, queryable by id, tag or category.</summary>
public interface IMobileTemplateService
{
    /// <summary>All registered mobile templates.</summary>
    IReadOnlyList<MobileTemplate> All { get; }

    /// <summary>Number of registered mobile templates.</summary>
    int Count { get; }

    /// <summary>Returns the mobile template with the given id (case-insensitive), or null.</summary>
    MobileTemplate? GetById(string id);

    /// <summary>Returns every template carrying the tag (case-insensitive).</summary>
    IEnumerable<MobileTemplate> GetByTag(string tag);

    /// <summary>Returns every template in the category (case-insensitive).</summary>
    IEnumerable<MobileTemplate> GetByCategory(string category);

    /// <summary>Adds or replaces a mobile template, indexed by id.</summary>
    void Register(MobileTemplate mobileTemplate);
}
