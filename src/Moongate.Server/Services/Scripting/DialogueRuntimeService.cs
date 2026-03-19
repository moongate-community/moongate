using System.Collections.Concurrent;
using System.Text;
using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Scripting;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Speech;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using MoonSharp.Interpreter;

namespace Moongate.Server.Services.Scripting;

/// <summary>
/// Manages active deterministic dialogue sessions and Lua-driven node transitions.
/// </summary>
public sealed class DialogueRuntimeService : IDialogueRuntimeService
{
    static DialogueRuntimeService()
    {
        UserData.RegisterType<DialogueContext>();
        UserData.RegisterType<LuaMobileRef>();
    }

    private readonly ConcurrentDictionary<(Serial NpcId, Serial ListenerId), DialogueSession> _sessions = [];
    private readonly IDialogueDefinitionService _dialogueDefinitionService;
    private readonly IDialogueMemoryService _dialogueMemoryService;
    private readonly ISpeechService _speechService;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    public DialogueRuntimeService(
        IDialogueDefinitionService dialogueDefinitionService,
        IDialogueMemoryService dialogueMemoryService,
        ISpeechService speechService,
        IGameNetworkSessionService gameNetworkSessionService
    )
    {
        _dialogueDefinitionService = dialogueDefinitionService;
        _dialogueMemoryService = dialogueMemoryService;
        _speechService = speechService;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    public async Task<DialogueSession?> StartAsync(
        UOMobileEntity npc,
        UOMobileEntity listener,
        string conversationId,
        string? topicId = null,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;

        if (!TryResolveDefinition(conversationId, out var definition))
        {
            return null;
        }

        var nodeId = definition.StartNodeId;

        if (!string.IsNullOrWhiteSpace(topicId) &&
            definition.TopicRoutes.TryGetValue(topicId.Trim(), out var topicNodeId))
        {
            nodeId = topicNodeId;
        }

        var session = new DialogueSession
        {
            NpcId = npc.Id,
            ListenerId = listener.Id,
            ConversationId = definition.ConversationId,
            CurrentNodeId = nodeId,
            LastTopicId = string.IsNullOrWhiteSpace(topicId) ? null : topicId.Trim(),
            StartedUtc = DateTime.UtcNow,
            LastUpdatedUtc = DateTime.UtcNow
        };

        _sessions[(npc.Id, listener.Id)] = session;

        return await EnterNodeAsync(npc, listener, definition, session, nodeId);
    }

    public async Task<DialogueSession?> HandleTopicAsync(
        UOMobileEntity npc,
        UOMobileEntity listener,
        string conversationId,
        string text,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;

        if (!TryResolveDefinition(conversationId, out var definition))
        {
            return null;
        }

        if (!TryMatchTopic(definition, text, out var topicId))
        {
            return null;
        }

        if (_sessions.TryGetValue((npc.Id, listener.Id), out var activeSession) &&
            string.Equals(activeSession.ConversationId, definition.ConversationId, StringComparison.OrdinalIgnoreCase))
        {
            activeSession.LastTopicId = topicId;
            return await EnterNodeAsync(npc, listener, definition, activeSession, definition.TopicRoutes[topicId]);
        }

        return await StartAsync(npc, listener, definition.ConversationId, topicId, cancellationToken);
    }

    public async Task<DialogueSession?> ChooseAsync(
        UOMobileEntity npc,
        UOMobileEntity listener,
        int optionIndex,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;

        if (optionIndex < 1 || !_sessions.TryGetValue((npc.Id, listener.Id), out var session))
        {
            return null;
        }

        if (!TryResolveDefinition(session.ConversationId, out var definition) ||
            optionIndex > session.VisibleOptions.Count)
        {
            return null;
        }

        var option = session.VisibleOptions[optionIndex - 1];
        var memory = _dialogueMemoryService.GetOrCreateEntry(npc.Id, listener.Id);
        var context = CreateContext(npc, listener, session, memory);

        if (option.Effects is not null)
        {
            option.Effects.OwnerScript.Call(option.Effects, UserData.Create(context));
        }

        if (context.EndRequested || string.Equals(option.Action, "end_conversation", StringComparison.OrdinalIgnoreCase))
        {
            PersistMemory(npc.Id, memory, session.CurrentNodeId, session.LastTopicId);
            EndSession(npc.Id, listener.Id);

            return null;
        }

        session.PendingAction = string.IsNullOrWhiteSpace(option.Action) ? null : option.Action.Trim();

        if (string.IsNullOrWhiteSpace(option.GotoNodeId))
        {
            PersistMemory(npc.Id, memory, session.CurrentNodeId, session.LastTopicId);
            session.LastUpdatedUtc = DateTime.UtcNow;
            _sessions[(npc.Id, listener.Id)] = session;

            return session;
        }

        return await EnterNodeAsync(npc, listener, definition, session, option.GotoNodeId!);
    }

    public bool TryGetActiveSession(Serial npcId, Serial listenerId, out DialogueSession? session)
    {
        session = null;

        if (_sessions.TryGetValue((npcId, listenerId), out var resolved))
        {
            session = resolved;
            return true;
        }

        return false;
    }

    public bool EndSession(Serial npcId, Serial listenerId)
        => _sessions.TryRemove((npcId, listenerId), out _);

    private DialogueContext CreateContext(
        UOMobileEntity npc,
        UOMobileEntity listener,
        DialogueSession session,
        DialogueMemoryEntry memory
    )
        => new(npc, listener, session, memory, _speechService, _gameNetworkSessionService);

    private async Task<DialogueSession?> EnterNodeAsync(
        UOMobileEntity npc,
        UOMobileEntity listener,
        DialogueDefinition definition,
        DialogueSession session,
        string nodeId
    )
    {
        if (!definition.Nodes.TryGetValue(nodeId, out var node))
        {
            EndSession(npc.Id, listener.Id);
            return null;
        }

        session.CurrentNodeId = node.NodeId;
        session.LastUpdatedUtc = DateTime.UtcNow;
        session.PendingAction = null;

        var memory = _dialogueMemoryService.GetOrCreateEntry(npc.Id, listener.Id);
        var context = CreateContext(npc, listener, session, memory);

        if (node.OnEnter is not null)
        {
            node.OnEnter.OwnerScript.Call(node.OnEnter, UserData.Create(context));
        }

        if (context.EndRequested)
        {
            PersistMemory(npc.Id, memory, node.NodeId, session.LastTopicId);
            EndSession(npc.Id, listener.Id);
            return null;
        }

        session.VisibleOptions = ResolveVisibleOptions(node, context);
        PersistMemory(npc.Id, memory, node.NodeId, session.LastTopicId);
        _sessions[(npc.Id, listener.Id)] = session;

        await Task.CompletedTask;

        return session;
    }

    private static List<DialogueOptionDefinition> ResolveVisibleOptions(DialogueNodeDefinition node, DialogueContext context)
    {
        var visibleOptions = new List<DialogueOptionDefinition>(node.Options.Count);

        foreach (var option in node.Options)
        {
            if (option.Condition is null)
            {
                visibleOptions.Add(option);
                continue;
            }

            var result = option.Condition.OwnerScript.Call(option.Condition, UserData.Create(context));

            if (IsTruthy(result))
            {
                visibleOptions.Add(option);
            }
        }

        return visibleOptions;
    }

    private void PersistMemory(Serial npcId, DialogueMemoryEntry memory, string nodeId, string? topicId)
    {
        memory.LastNode = nodeId;
        memory.LastTopic = topicId;
        memory.LastInteractionUtc = DateTime.UtcNow;
        _dialogueMemoryService.MarkDirty(npcId);
        _dialogueMemoryService.Save(npcId);
    }

    private static bool TryMatchTopic(DialogueDefinition definition, string text, out string topicId)
    {
        topicId = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var tokens = Tokenize(text);

        foreach (var topic in definition.Topics)
        {
            if (!definition.TopicRoutes.ContainsKey(topic.Key))
            {
                continue;
            }

            foreach (var alias in topic.Value)
            {
                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                if (tokens.Contains(alias, StringComparer.OrdinalIgnoreCase))
                {
                    topicId = topic.Key;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsTruthy(DynValue value)
        => value.Type switch
        {
            DataType.Nil or DataType.Void => false,
            DataType.Boolean => value.Boolean,
            _ => true
        };

    private static HashSet<string> Tokenize(string text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var builder = new StringBuilder(text.Length);

        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                builder.Append(char.ToLowerInvariant(ch));
                continue;
            }

            FlushToken(tokens, builder);
        }

        FlushToken(tokens, builder);

        return tokens;
    }

    private static void FlushToken(HashSet<string> tokens, StringBuilder builder)
    {
        if (builder.Length == 0)
        {
            return;
        }

        tokens.Add(builder.ToString());
        builder.Clear();
    }

    private bool TryResolveDefinition(string conversationId, out DialogueDefinition definition)
    {
        definition = null!;

        if (!_dialogueDefinitionService.TryGet(conversationId, out var resolved) || resolved is null)
        {
            return false;
        }

        definition = resolved;
        return true;
    }
}
