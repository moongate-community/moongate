using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Mobiles.Templates;

/// <summary>A declarative mobile spawn template. Inheritance (BaseMobile) is resolved at load time.</summary>
public sealed class MobileTemplate
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The <c>names.yaml</c> pool this template draws a name from when it has no <see cref="Name" />
    /// of its own. A literal pool type — the shipped pools spell gender inconsistently
    /// (<c>male</c>, <c>tokuno male</c>, <c>male elf brigand</c>), so nothing is appended for you.
    /// </summary>
    public string NamePool { get; set; } = string.Empty;

    public MobileTemplateGenderType Gender { get; set; } = MobileTemplateGenderType.Male;

    public string Title { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public string? BaseMobile { get; set; }

    public int Strength { get; set; } = 50;

    public int Dexterity { get; set; } = 50;

    public int Intelligence { get; set; } = 50;

    public Dictionary<string, int> Skills { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public MobileAppearance Appearance { get; set; } = new();

    public List<MobileEquipmentEntry> Equipment { get; set; } = [];

    public List<MobileVariant> Variants { get; set; } = [];

    public string? LootTableId { get; set; }

    public string? BrainScript { get; set; }
}
