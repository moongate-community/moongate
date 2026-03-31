namespace Moongate.UO.Data.Templates.Quests;

/// <summary>
/// Serializable definition of a quest template.
/// </summary>
public class QuestTemplateDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<string> QuestGiverTemplateIds { get; set; } = [];

    public List<string> CompletionNpcTemplateIds { get; set; } = [];

    public bool Repeatable { get; set; }

    public int MaxActivePerCharacter { get; set; }

    public List<QuestObjectiveDefinition> Objectives { get; set; } = [];

    public List<QuestRewardDefinition> Rewards { get; set; } = [];
}
