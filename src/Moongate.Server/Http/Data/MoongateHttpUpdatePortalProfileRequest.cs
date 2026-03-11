namespace Moongate.Server.Http.Data;

/// <summary>
/// Editable fields for the authenticated portal account.
/// </summary>
public sealed class MoongateHttpUpdatePortalProfileRequest
{
    public string? Email { get; init; }
}
