namespace Moongate.Server.Data.Config;

/// <summary>
/// Large language model configuration for intelligent NPC dialogue.
/// </summary>
public sealed class MoongateLlmConfig
{
    /// <summary>
    /// Enables OpenAI-backed NPC dialogue.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// OpenAI API key. When omitted, OPENAI_API_KEY is used as fallback.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Chat model used for NPC dialogue generation.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Optional custom OpenAI base URL.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Cooldown between player-triggered dialogue requests for the same NPC.
    /// </summary>
    public int ListenerCooldownMilliseconds { get; set; } = 60_000;

    /// <summary>
    /// Cooldown between idle chatter requests for the same NPC.
    /// </summary>
    public int IdleCooldownMilliseconds { get; set; } = 300_000;

    /// <summary>
    /// Nearby-player range required before idle chatter is allowed.
    /// </summary>
    public int IdleNearbyPlayerRange { get; set; } = 12;

    /// <summary>
    /// Speech broadcast range used when the NPC speaks.
    /// </summary>
    public int SpeechRange { get; set; } = 12;

    /// <summary>
    /// Maximum memory characters injected into the model prompt.
    /// </summary>
    public int MaxMemoryCharacters { get; set; } = 4_000;

    /// <summary>
    /// Maximum output tokens allowed for one dialogue completion.
    /// </summary>
    public int MaxOutputTokenCount { get; set; } = 1_200;
}
