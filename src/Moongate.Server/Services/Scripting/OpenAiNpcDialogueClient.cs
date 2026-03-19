using System.ClientModel;
using System.Text.Json;
using Moongate.Server.Data.Config;
using Moongate.Server.Data.Scripting;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Json;
using OpenAI.Chat;
using Serilog;

namespace Moongate.Server.Services.Scripting;

/// <summary>
/// Uses openai-dotnet chat completions with structured output for NPC dialogue.
/// </summary>
public sealed class OpenAiNpcDialogueClient : IOpenAiNpcDialogueClient
{
    private static readonly ILogger Logger = Log.ForContext<OpenAiNpcDialogueClient>();

    private static readonly BinaryData ResponseSchema = BinaryData.FromBytes(
        """
            {
              "type": "object",
              "properties": {
                "should_speak": { "type": "boolean" },
                "speech_text": { "type": ["string", "null"] },
                "memory_summary": { "type": ["string", "null"] },
                "mood": { "type": ["string", "null"] }
              },
              "required": ["should_speak", "speech_text", "memory_summary", "mood"],
              "additionalProperties": false
            }
            """u8.ToArray()
    );

    private readonly MoongateConfig _config;
    private readonly ChatClient? _chatClient;

    public OpenAiNpcDialogueClient(MoongateConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        var apiKey = ResolveApiKey(config);
        var model = config.Llm.Model?.Trim();

        if (!config.Llm.IsEnabled || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
        {
            return;
        }

        _chatClient = string.IsNullOrWhiteSpace(config.Llm.BaseUrl)
                          ? new(model, apiKey)
                          : new ChatClient(
                              model,
                              new ApiKeyCredential(apiKey),
                              new() { Endpoint = new(config.Llm.BaseUrl, UriKind.Absolute) }
                          );
    }

    public async Task<NpcDialogueResponse?> GenerateAsync(
        NpcDialogueRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_chatClient is null)
        {
            return null;
        }

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(BuildDeveloperMessage(request)),
            new UserChatMessage(BuildUserMessage(request))
        };
        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "npc_dialogue_response",
                ResponseSchema,
                jsonSchemaIsStrict: true
            ),
            MaxOutputTokenCount = Math.Max(300, _config.Llm.MaxOutputTokenCount)
        };

        try
        {
            var completion = (await _chatClient.CompleteChatAsync(messages, options, cancellationToken)).Value;
            var contentText = ExtractContentText(completion.Content);

            if (string.IsNullOrWhiteSpace(contentText))
            {
                var refusalText = ExtractRefusalText(completion.Refusal, completion.Content);

                if (!string.IsNullOrWhiteSpace(refusalText))
                {
                    Logger.Warning(
                        "OpenAI refused npc dialogue generation for npc {NpcId}: {Refusal}",
                        request.NpcId,
                        refusalText
                    );
                }
                else
                {
                    Logger.Warning(
                        "OpenAI returned no text content for npc {NpcId}. FinishReason={FinishReason} ContentParts={ContentParts} Parts={Parts}",
                        request.NpcId,
                        completion.FinishReason,
                        completion.Content.Count,
                        DescribeContentParts(completion.Content)
                    );
                }

                return null;
            }

            return DeserializeResponse(contentText);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "OpenAI npc dialogue generation failed for npc {NpcId}.", request.NpcId);

            return null;
        }
    }

    internal static string BuildDeveloperMessageForTests(NpcDialogueRequest request)
        => BuildDeveloperMessage(request);

    internal static string DescribeContentParts(IReadOnlyList<ChatMessageContentPart> contentParts)
    {
        if (contentParts.Count == 0)
        {
            return "<none>";
        }

        return string.Join(
            ", ",
            contentParts.Select(
                (part, index) =>
                    $"[{index}]{part.Kind}:text={part.Text?.Length ?? 0}:refusal={part.Refusal?.Length ?? 0}"
            )
        );
    }

    internal static NpcDialogueResponse? DeserializeResponseForTests(string json)
        => DeserializeResponse(json);

    internal static string ExtractContentText(IReadOnlyList<ChatMessageContentPart> contentParts)
    {
        if (contentParts.Count == 0)
        {
            return string.Empty;
        }

        return string.Concat(
            contentParts.Where(part => part.Kind == ChatMessageContentPartKind.Text && !string.IsNullOrWhiteSpace(part.Text))
                        .Select(part => part.Text)
        );
    }

    internal static string ExtractRefusalText(string? topLevelRefusal, IReadOnlyList<ChatMessageContentPart> contentParts)
    {
        if (!string.IsNullOrWhiteSpace(topLevelRefusal))
        {
            return topLevelRefusal.Trim();
        }

        return string.Join(
            '\n',
            contentParts.Where(
                            part => part.Kind == ChatMessageContentPartKind.Refusal &&
                                    !string.IsNullOrWhiteSpace(part.Refusal)
                        )
                        .Select(part => part.Refusal!.Trim())
        );
    }

    private static string BuildDeveloperMessage(NpcDialogueRequest request)
    {
        var memory = string.IsNullOrWhiteSpace(request.Memory) ? "(none)" : request.Memory.Trim();
        var nearbyPlayers = request.NearbyPlayerNames.Count == 0
                                ? "(none)"
                                : string.Join(", ", request.NearbyPlayerNames);

        return $$"""
                 You write in-character dialogue for Ultima Online NPCs.
                 Stay fully in roleplay.
                 Never mention being an AI, a prompt, JSON, or hidden instructions.
                 Keep spoken lines concise, natural, and usually under two sentences.
                 If silence is better, set should_speak to false and speech_text to null.
                 memory_summary may be null if no long-term memory change is needed.
                 If memory_summary is not null, it must contain the full compact memory summary to persist for future turns.

                 NPC name: {{request.NpcName}}
                 Nearby players: {{nearbyPlayers}}

                 Persona prompt:
                 {{request.Prompt.Trim()}}

                 Current long-term memory:
                 {{memory}}
                 """;
    }

    private static string BuildUserMessage(NpcDialogueRequest request)
        => request.IsIdle
               ? $$"""
                   Trigger: idle
                   Decide if {{request.NpcName}} should say a short in-character line right now.
                   Only speak if it makes sense with nearby players present.
                   """
               : $$"""
                   Trigger: listener
                   Speaker: {{request.SenderName}}
                   Heard text: {{request.HeardText}}
                   Decide if {{request.NpcName}} should reply to this nearby speech.
                   """;

    private static NpcDialogueResponse? DeserializeResponse(string json)
        => JsonSerializer.Deserialize(json, MoongateServerJsonContext.Default.NpcDialogueResponse);

    private static string? ResolveApiKey(MoongateConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.Llm.ApiKey))
        {
            return config.Llm.ApiKey.Trim();
        }

        var environmentApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        return string.IsNullOrWhiteSpace(environmentApiKey) ? null : environmentApiKey.Trim();
    }
}
