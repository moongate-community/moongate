namespace Moongate.UO.Data.Templates.Quests;

/// <summary>
/// Serializable definition of a quest reward item.
/// </summary>
public class QuestRewardItemDefinition
{
    public string ItemTemplateId { get; set; } = string.Empty;

    public int Amount { get; set; }
}
