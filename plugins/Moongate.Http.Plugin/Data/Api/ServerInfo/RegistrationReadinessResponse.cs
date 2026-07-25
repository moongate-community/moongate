namespace Moongate.Http.Plugin.Data.Api.ServerInfo;

/// <summary>The local prerequisites behind public account registration availability.</summary>
public sealed record RegistrationReadinessResponse(
    bool Ready,
    bool WebsiteValid,
    bool EmailChannelSelected,
    bool EmailChannelAvailable
);
