using Moongate.UO.Data.Templates.Quests;

namespace Moongate.Server.Data.Scripting;

/// <summary>
/// Registered Lua-authored quest definition compiled from the quest DSL.
/// </summary>
public sealed class QuestLuaDefinition
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

    public int RewardGold { get; set; }

    public List<QuestRewardItemDefinition> RewardItems { get; set; } = [];

    public string? ScriptPath { get; set; }

    public QuestTemplateDefinition Compile()
    {
        var template = new QuestTemplateDefinition
        {
            Id = Id,
            Name = Name,
            Category = Category,
            Description = Description,
            QuestGiverTemplateIds = [.. QuestGiverTemplateIds],
            CompletionNpcTemplateIds = [.. CompletionNpcTemplateIds],
            Repeatable = Repeatable,
            MaxActivePerCharacter = MaxActivePerCharacter,
            Objectives =
            [
                ..Objectives.Select(
                    static objective => new QuestObjectiveDefinition
                    {
                        Type = objective.Type,
                        ItemTemplateId = objective.ItemTemplateId,
                        MobileTemplateIds = [.. objective.MobileTemplateIds],
                        Amount = objective.Amount
                    }
                )
            ]
        };

        if (RewardGold > 0 || RewardItems.Count > 0)
        {
            template.Rewards.Add(
                new QuestRewardDefinition
                {
                    Gold = RewardGold,
                    Items =
                    [
                        ..RewardItems.Select(
                            static item => new QuestRewardItemDefinition
                            {
                                ItemTemplateId = item.ItemTemplateId,
                                Amount = item.Amount
                            }
                        )
                    ]
                }
            );
        }

        return template;
    }
}
