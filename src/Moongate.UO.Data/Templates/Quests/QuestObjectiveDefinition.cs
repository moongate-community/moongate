using System.Linq;
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

    public string ObjectiveId
        => Type switch
        {
            QuestObjectiveType.Kill => BuildKillObjectiveId(),
            QuestObjectiveType.Collect => BuildItemObjectiveId("collect", ItemTemplateId),
            QuestObjectiveType.Deliver => BuildItemObjectiveId("deliver", ItemTemplateId),
            _ => Type.ToString()
        };

    private string BuildKillObjectiveId()
        => BuildObjectiveId(
            "kill",
            Amount,
            MobileTemplateIds.Select(static templateId => string.IsNullOrWhiteSpace(templateId) ? string.Empty : templateId.Trim())
                             .Where(static templateId => !string.IsNullOrWhiteSpace(templateId))
                             .OrderBy(static templateId => templateId, StringComparer.OrdinalIgnoreCase)
        );

    private string BuildItemObjectiveId(string objectiveType, string? itemTemplateId)
        => BuildObjectiveId(objectiveType, Amount, [NormalizeTemplateId(itemTemplateId)]);

    private static string BuildObjectiveId(string objectiveType, int amount, IEnumerable<string?> templateIds)
        => $"{objectiveType}:{amount}:{string.Join(',', templateIds.Where(static templateId => !string.IsNullOrWhiteSpace(templateId)).Select(static templateId => templateId!.ToLowerInvariant()))}";

    private static string NormalizeTemplateId(string? templateId)
        => string.IsNullOrWhiteSpace(templateId) ? string.Empty : templateId.Trim();
}
