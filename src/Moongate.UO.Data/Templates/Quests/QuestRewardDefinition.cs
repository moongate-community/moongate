namespace Moongate.UO.Data.Templates.Quests;

/// <summary>
/// Serializable definition of a quest reward bundle.
/// </summary>
public class QuestRewardDefinition
{
    public int Gold { get; set; }

    public List<QuestRewardItemDefinition> Items { get; set; } = [];
}
