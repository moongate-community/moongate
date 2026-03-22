namespace Moongate.UO.Data.Templates.Factions;

/// <summary>
/// Serializable definition of a faction and its hostile relationships.
/// </summary>
public sealed class FactionDefinition : FactionDefinitionBase
{
    public List<string> EnemyFactionIds { get; set; } = [];
}
