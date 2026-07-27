using DryIoc;
using Moongate.Server.Abstractions.Extensions;
using Moongate.Smtp.Plugin.Data.Config;
using Moongate.Smtp.Plugin.Interfaces;
using Moongate.Smtp.Plugin.Services;
using Serilog;
using SquidStd.Abstractions.Extensions.Config;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.Smtp.Plugin;

/// <summary>Registers the email notification channel, when SMTP has been configured.</summary>
public class MoongateSmtpPlugin : ISquidStdPlugin
{
    private const string PasswordEnvironmentVariable = "MOONGATE_SMTP_PASSWORD";

    public PluginMetadata Metadata
        => new()
        {
            Id = "moongate.smtp.plugin",
            Version = new(VersionUtils.GetVersion(typeof(MoongateSmtpPlugin).Assembly)),
            Author = "squid",
            Name = "Moongate SMTP",
            Description = "Email delivery for the notification pipeline"
        };

    public void Configure(IContainer container, PluginContext context)
    {
        var directories = container.Resolve<DirectoriesConfig>();
        container.RegisterConfigFile<MoongateSmtpConfig>("smtp", directories["plugins/configs"]);

        var config = container.Resolve<MoongateSmtpConfig>();

        // A container deployment supplies secrets as environment variables, not as a mounted file.
        if (Environment.GetEnvironmentVariable(PasswordEnvironmentVariable) is { Length: > 0 } password)
        {
            config.Password = password;
        }

        var logger = Log.ForContext<MoongateSmtpPlugin>();

        // Registering an unconfigured channel would be worse than registering none: every notification
        // addressed to "email" would spend the pipeline's retries connecting to an empty host. Left
        // unregistered, it produces one clear "no such channel" warning instead.
        if (string.IsNullOrWhiteSpace(config.Host) || string.IsNullOrWhiteSpace(config.FromAddress))
        {
            logger.Warning(
                "SMTP is not configured (Host and FromAddress are required in plugins/configs/smtp.yaml); the email notification channel is not registered"
            );

            return;
        }

        container.Register<ISmtpTransport, MailKitSmtpTransport>(Reuse.Singleton);
        container.RegisterNotificationChannel<SmtpNotificationChannel>();

        logger.Information("Email notifications will be sent through {Host}:{Port}", config.Host, config.Port);
    }
}
