using System.Collections.Concurrent;
using Moongate.Server.Data.Scripting;
using Moongate.Server.Interfaces.Services.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Server.Services.Scripting;

/// <summary>
/// In-memory registry of authored dialogue definitions loaded from Lua.
/// </summary>
public sealed class DialogueDefinitionService : IDialogueDefinitionService
{
    private readonly ConcurrentDictionary<string, DialogueDefinition> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    public bool Register(string conversationId, Table? definition, string? scriptPath = null)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || definition is null)
        {
            return false;
        }

        var normalizedConversationId = conversationId.Trim();
        var parsed = ParseDefinition(normalizedConversationId, definition, scriptPath);
        _definitions[normalizedConversationId] = parsed;

        return true;
    }

    public bool TryGet(string conversationId, out DialogueDefinition? definition)
    {
        definition = null;

        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return false;
        }

        if (_definitions.TryGetValue(conversationId.Trim(), out var resolved))
        {
            definition = resolved;

            return true;
        }

        return false;
    }

    private static DialogueDefinition ParseDefinition(string conversationId, Table definition, string? scriptPath)
    {
        var startNodeId = RequireString(definition, "start", $"Conversation '{conversationId}' is missing 'start'.");
        var parsed = new DialogueDefinition
        {
            ConversationId = conversationId,
            StartNodeId = startNodeId,
            ScriptPath = string.IsNullOrWhiteSpace(scriptPath) ? null : scriptPath.Trim()
        };

        ParseTopics(definition, parsed);
        ParseTopicRoutes(definition, parsed);
        ParseNodes(definition, parsed);

        if (!parsed.Nodes.ContainsKey(parsed.StartNodeId))
        {
            throw new InvalidOperationException(
                $"Conversation '{conversationId}' references missing start node '{parsed.StartNodeId}'."
            );
        }

        foreach (var route in parsed.TopicRoutes)
        {
            if (!parsed.Nodes.ContainsKey(route.Value))
            {
                throw new InvalidOperationException(
                    $"Conversation '{conversationId}' topic route '{route.Key}' references missing node '{route.Value}'."
                );
            }
        }

        foreach (var node in parsed.Nodes.Values)
        {
            foreach (var option in node.Options.Where(static option => !string.IsNullOrWhiteSpace(option.GotoNodeId)))
            {
                if (!parsed.Nodes.ContainsKey(option.GotoNodeId!))
                {
                    throw new InvalidOperationException(
                        $"Conversation '{conversationId}' node '{node.NodeId}' option '{option.Text}' references missing goto node '{option.GotoNodeId}'."
                    );
                }
            }
        }

        return parsed;
    }

    private static void ParseNodes(Table definition, DialogueDefinition parsed)
    {
        var nodesTable = RequireTable(
            definition,
            "nodes",
            $"Conversation '{parsed.ConversationId}' is missing 'nodes'."
        );

        foreach (var pair in nodesTable.Pairs)
        {
            if (pair.Key.Type != DataType.String || pair.Value.Type != DataType.Table)
            {
                continue;
            }

            var nodeId = pair.Key.String;
            var nodeTable = pair.Value.Table;

            if (string.IsNullOrWhiteSpace(nodeId) || nodeTable is null)
            {
                continue;
            }

            var nodeDefinition = new DialogueNodeDefinition
            {
                NodeId = nodeId.Trim(),
                Text = RequireString(
                    nodeTable,
                    "text",
                    $"Conversation '{parsed.ConversationId}' node '{nodeId}' is missing 'text'."
                ),
                OnEnter = ResolveOptionalFunction(nodeTable, "on_enter")
            };

            var optionsValue = nodeTable.Get("options");

            if (optionsValue.Type == DataType.Table && optionsValue.Table is not null)
            {
                foreach (var optionPair in optionsValue.Table.Pairs.OrderBy(static pair => pair.Key.CastToNumber()))
                {
                    if (optionPair.Value.Type != DataType.Table || optionPair.Value.Table is null)
                    {
                        continue;
                    }

                    nodeDefinition.Options.Add(ParseOption(parsed.ConversationId, nodeId, optionPair.Value.Table));
                }
            }

            parsed.Nodes[nodeDefinition.NodeId] = nodeDefinition;
        }

        if (parsed.Nodes.Count == 0)
        {
            throw new InvalidOperationException($"Conversation '{parsed.ConversationId}' does not define any nodes.");
        }
    }

    private static DialogueOptionDefinition ParseOption(string conversationId, string nodeId, Table optionTable)
    {
        var option = new DialogueOptionDefinition
        {
            Text = RequireString(
                optionTable,
                "text",
                $"Conversation '{conversationId}' node '{nodeId}' contains an option without 'text'."
            ),
            GotoNodeId = ResolveOptionalString(optionTable, "goto") ?? ResolveOptionalString(optionTable, "goto_"),
            Action = ResolveOptionalString(optionTable, "action"),
            Condition = ResolveOptionalFunction(optionTable, "condition"),
            Effects = ResolveOptionalFunction(optionTable, "effects")
        };

        if (string.IsNullOrWhiteSpace(option.GotoNodeId) && string.IsNullOrWhiteSpace(option.Action))
        {
            throw new InvalidOperationException(
                $"Conversation '{conversationId}' node '{nodeId}' option '{option.Text}' must define either 'goto' or 'action'."
            );
        }

        return option;
    }

    private static void ParseTopics(Table definition, DialogueDefinition parsed)
    {
        var topicsValue = definition.Get("topics");

        if (topicsValue.Type != DataType.Table || topicsValue.Table is null)
        {
            return;
        }

        foreach (var pair in topicsValue.Table.Pairs)
        {
            if (pair.Key.Type != DataType.String || pair.Value.Type != DataType.Table || pair.Value.Table is null)
            {
                continue;
            }

            var topicId = pair.Key.String?.Trim();

            if (string.IsNullOrWhiteSpace(topicId))
            {
                continue;
            }

            var aliases = pair.Value.Table.Pairs
                              .OrderBy(static item => item.Key.CastToNumber())
                              .Where(static item => item.Value.Type == DataType.String)
                              .Select(static item => item.Value.String?.Trim())
                              .Where(static value => !string.IsNullOrWhiteSpace(value))
                              .Cast<string>()
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .ToArray();

            parsed.Topics[topicId] = aliases;
        }
    }

    private static void ParseTopicRoutes(Table definition, DialogueDefinition parsed)
    {
        var routesValue = definition.Get("topic_routes");

        if (routesValue.Type != DataType.Table || routesValue.Table is null)
        {
            return;
        }

        foreach (var pair in routesValue.Table.Pairs)
        {
            if (pair.Key.Type != DataType.String || pair.Value.Type != DataType.String)
            {
                continue;
            }

            var topicId = pair.Key.String?.Trim();
            var targetNodeId = pair.Value.String?.Trim();

            if (string.IsNullOrWhiteSpace(topicId) || string.IsNullOrWhiteSpace(targetNodeId))
            {
                continue;
            }

            parsed.TopicRoutes[topicId] = targetNodeId;
        }
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

    private static Table RequireTable(Table table, string key, string message)
    {
        var value = table.Get(key);

        if (value.Type != DataType.Table || value.Table is null)
        {
            throw new InvalidOperationException(message);
        }

        return value.Table;
    }

    private static string? ResolveOptionalString(Table table, string key)
    {
        var value = table.Get(key);

        if (value.Type != DataType.String || string.IsNullOrWhiteSpace(value.String))
        {
            return null;
        }

        return value.String.Trim();
    }

    private static Closure? ResolveOptionalFunction(Table table, string key)
    {
        var value = table.Get(key);

        return value.Type == DataType.Function ? value.Function : null;
    }
}
