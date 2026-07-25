namespace Moongate.Http.Plugin.Data.Registration;

/// <summary>The local prerequisites that make public account registration available.</summary>
public sealed record RegistrationReadiness(
    bool WebsiteValid,
    bool EmailChannelSelected,
    bool EmailChannelAvailable
)
{
    /// <summary>Whether every local registration prerequisite is met.</summary>
    public bool Ready => WebsiteValid && EmailChannelSelected && EmailChannelAvailable;
}
