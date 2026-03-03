namespace Moongate.Server.Data.Config;

public class MoongateEmailConfig
{
    public bool IsEnabled { get; set; } = false;

    public string FromAddress { get; set; } = "no-reply@moongate.local";

    public string FallbackLocale { get; set; } = "en";

    public MoongateEmailSmtpConfig Smtp { get; set; } = new();
}
