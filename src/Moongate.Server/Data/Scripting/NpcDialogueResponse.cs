using System.Text.Json.Serialization;

namespace Moongate.Server.Data.Scripting;

/// <summary>
/// Structured output returned by the OpenAI NPC dialogue client.
/// </summary>
public sealed record NpcDialogueResponse
{
    [JsonPropertyName("should_speak")]
    public bool ShouldSpeak { get; init; }

    [JsonPropertyName("speech_text")]
    public string? SpeechText { get; init; }

    [JsonPropertyName("memory_summary")]
    public string? MemorySummary { get; init; }

    [JsonPropertyName("mood")]
    public string? Mood { get; init; }
}
