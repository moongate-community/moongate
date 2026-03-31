using System.Collections.Concurrent;
using Moongate.Server.Data.Scripting;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.UO.Data.Templates.Quests;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;

namespace Moongate.Server.Services.Scripting;

/// <summary>
/// In-memory registry of Lua-authored quest definitions.
/// </summary>
public sealed class QuestDefinitionService : IQuestDefinitionService
{
    private readonly ConcurrentDictionary<string, QuestLuaDefinition> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    public void Clear()
        => _definitions.Clear();

    public IReadOnlyList<QuestLuaDefinition> GetAll()
        => _definitions.Values.OrderBy(static definition => definition.Id, StringComparer.OrdinalIgnoreCase).ToList();

    public bool Register(Table? definition, string? scriptPath = null)
    {
        if (definition is null)
        {
            return false;
        }

        var parsed = ParseDefinition(definition, scriptPath);

        if (!_definitions.TryAdd(parsed.Id, parsed))
        {
            throw new InvalidOperationException($"Quest '{parsed.Id}' is already registered.");
        }

        return true;
    }

    public bool TryGet(string questId, out QuestLuaDefinition? definition)
    {
        definition = null;

        if (string.IsNullOrWhiteSpace(questId))
        {
            return false;
        }

        if (_definitions.TryGetValue(questId.Trim(), out var resolved))
        {
            definition = resolved;

            return true;
        }

        return false;
    }

    private static QuestLuaDefinition ParseDefinition(Table definition, string? scriptPath)
    {
        var questId = RequireString(definition, "id", "Quest definition is missing 'id'.");
        var quest = new QuestLuaDefinition
        {
            Id = questId,
            Name = RequireString(definition, "name", $"Quest '{questId}' is missing 'name'."),
            Category = RequireString(definition, "category", $"Quest '{questId}' is missing 'category'."),
            Description = RequireString(definition, "description", $"Quest '{questId}' is missing 'description'."),
            QuestGiverTemplateIds = RequireStringArray(
                definition,
                "quest_givers",
                $"Quest '{questId}' requires at least one quest giver.",
                $"Quest '{questId}' contains invalid 'quest_givers' entry."
            ),
            CompletionNpcTemplateIds = RequireStringArray(
                definition,
                "completion_npcs",
                $"Quest '{questId}' requires at least one completion npc.",
                $"Quest '{questId}' contains invalid 'completion_npcs' entry."
            ),
            Repeatable = ResolveOptionalBool(definition, "repeatable") ?? false,
            MaxActivePerCharacter = ResolveRequiredPositiveInt(
                definition,
                "max_active_per_character",
                $"Quest '{questId}' requires positive 'max_active_per_character'."
            ),
            ScriptPath = string.IsNullOrWhiteSpace(scriptPath) ? null : scriptPath.Trim()
        };

        ParseObjectives(quest, definition);
        ParseRewards(quest, definition);

        return quest;
    }

    private static void ParseObjectives(QuestLuaDefinition quest, Table definition)
    {
        var objectivesTable = RequireTable(
            definition,
            "objectives",
            $"Quest '{quest.Id}' is missing 'objectives'."
        );

        foreach (var pair in objectivesTable.Pairs.OrderBy(static item => item.Key.CastToNumber()))
        {
            if (pair.Value.Type != DataType.Table || pair.Value.Table is null)
            {
                throw new InvalidOperationException($"Quest '{quest.Id}' contains invalid objective shape.");
            }

            quest.Objectives.Add(ParseObjective(quest.Id, pair.Value.Table));
        }

        if (quest.Objectives.Count == 0)
        {
            throw new InvalidOperationException($"Quest '{quest.Id}' must define at least one objective.");
        }
    }

    private static QuestObjectiveDefinition ParseObjective(string questId, Table objectiveTable)
    {
        var typeText = RequireString(objectiveTable, "type", $"Quest '{questId}' objective is missing 'type'.");

        if (!Enum.TryParse<QuestObjectiveType>(typeText, true, out var objectiveType))
        {
            throw new InvalidOperationException(
                $"Quest '{questId}' has unsupported objective type '{typeText}'."
            );
        }

        var objective = new QuestObjectiveDefinition
        {
            Type = objectiveType,
            Amount = RequirePositiveInt(
                objectiveTable,
                "amount",
                $"Quest '{questId}' {typeText.ToLowerInvariant()} objective requires positive 'amount'."
            )
        };

        switch (objectiveType)
        {
            case QuestObjectiveType.Kill:
                objective.MobileTemplateIds = RequireStringArray(
                    objectiveTable,
                    "mobiles",
                    $"Quest '{questId}' kill objective requires 'mobiles'.",
                    $"Quest '{questId}' kill objective contains invalid 'mobiles' entry."
                );
                break;
            case QuestObjectiveType.Collect:
                objective.ItemTemplateId = RequireString(
                    objectiveTable,
                    "item_template_id",
                    $"Quest '{questId}' collect objective requires 'item_template_id'."
                );
                break;
            case QuestObjectiveType.Deliver:
                objective.ItemTemplateId = RequireString(
                    objectiveTable,
                    "item_template_id",
                    $"Quest '{questId}' deliver objective requires 'item_template_id'."
                );
                break;
        }

        return objective;
    }

    private static void ParseRewards(QuestLuaDefinition quest, Table definition)
    {
        var rewardsValue = definition.Get("rewards");

        if (rewardsValue.Type == DataType.Nil || rewardsValue.Type == DataType.Void)
        {
            return;
        }

        if (rewardsValue.Type != DataType.Table || rewardsValue.Table is null)
        {
            throw new InvalidOperationException($"Quest '{quest.Id}' has invalid 'rewards'.");
        }

        foreach (var pair in rewardsValue.Table.Pairs.OrderBy(static item => item.Key.CastToNumber()))
        {
            if (pair.Value.Type != DataType.Table || pair.Value.Table is null)
            {
                throw new InvalidOperationException($"Quest '{quest.Id}' contains invalid reward shape.");
            }

            ParseReward(quest, pair.Value.Table);
        }
    }

    private static void ParseReward(QuestLuaDefinition quest, Table rewardTable)
    {
        var rewardType = RequireString(rewardTable, "type", $"Quest '{quest.Id}' reward is missing 'type'.");
        var amount = RequirePositiveInt(
            rewardTable,
            "amount",
            $"Quest '{quest.Id}' reward '{rewardType}' requires positive 'amount'."
        );

        switch (rewardType.Trim().ToLowerInvariant())
        {
            case "gold":
                quest.RewardGold += amount;
                break;
            case "item":
                quest.RewardItems.Add(
                    new QuestRewardItemDefinition
                    {
                        ItemTemplateId = RequireString(
                            rewardTable,
                            "item_template_id",
                            $"Quest '{quest.Id}' item reward requires 'item_template_id'."
                        ),
                        Amount = amount
                    }
                );
                break;
            default:
                throw new InvalidOperationException(
                    $"Quest '{quest.Id}' has unsupported reward type '{rewardType}'."
                );
        }
    }

    private static bool? ResolveOptionalBool(Table table, string key)
    {
        var value = table.Get(key);

        return value.Type == DataType.Boolean ? value.Boolean : null;
    }

    private static int ResolveRequiredPositiveInt(Table table, string key, string message)
    {
        var value = table.Get(key);

        if (value.Type != DataType.Number)
        {
            throw new InvalidOperationException(message);
        }

        var parsed = (int)value.Number;

        if (parsed <= 0)
        {
            throw new InvalidOperationException(message);
        }

        return parsed;
    }

    private static int RequirePositiveInt(Table table, string key, string message)
    {
        var value = table.Get(key);

        if (value.Type != DataType.Number)
        {
            throw new InvalidOperationException(message);
        }

        var parsed = (int)value.Number;

        if (parsed <= 0)
        {
            throw new InvalidOperationException(message);
        }

        return parsed;
    }

    private static string RequireString(Table table, string key, string message)
    {
        var value = table.Get(key);

        if (value.Type != DataType.String || string.IsNullOrWhiteSpace(value.String))
        {
            throw new InvalidOperationException(message);
        }

        return value.String.Trim();
    }

    private static List<string> RequireStringArray(
        Table table,
        string key,
        string missingMessage,
        string invalidEntryMessage
    )
    {
        var values = ResolveStringArray(table, key, invalidEntryMessage);

        if (values.Count == 0)
        {
            throw new InvalidOperationException(missingMessage);
        }

        return values;
    }

    private static List<string> ResolveStringArray(Table table, string key, string invalidEntryMessage)
    {
        var value = table.Get(key);

        if (value.Type != DataType.Table || value.Table is null)
        {
            return [];
        }

        var resolved = new List<string>();

        foreach (var pair in value.Table.Pairs.OrderBy(static pair => pair.Key.CastToNumber()))
        {
            if (pair.Value.Type != DataType.String || string.IsNullOrWhiteSpace(pair.Value.String))
            {
                throw new InvalidOperationException(invalidEntryMessage);
            }

            var normalized = pair.Value.String.Trim();

            if (resolved.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            resolved.Add(normalized);
        }

        return resolved;
    }

    private static Table RequireTable(Table table, string key, string message)
    {
        var value = table.Get(key);

        if (value.Type != DataType.Table || value.Table is null)
        {
            throw new InvalidOperationException(message);
        }

        return value.Table;
    }
}
