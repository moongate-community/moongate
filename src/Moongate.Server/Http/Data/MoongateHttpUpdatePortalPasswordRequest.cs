namespace Moongate.Server.Http.Data;

public class MoongateHttpUpdatePortalPasswordRequest
{
    public string? CurrentPassword { get; set; }

    public string? NewPassword { get; set; }

    public string? ConfirmPassword { get; set; }
}
