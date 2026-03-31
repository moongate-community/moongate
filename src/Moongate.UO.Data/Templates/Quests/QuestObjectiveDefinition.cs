using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Templates.Quests;

/// <summary>
/// Serializable definition of a quest objective.
/// </summary>
public class QuestObjectiveDefinition
{
    public QuestObjectiveType Type { get; set; }

    public string? ItemTemplateId { get; set; }

    public List<string> MobileTemplateIds { get; set; } = [];

    public int Amount { get; set; }
}
