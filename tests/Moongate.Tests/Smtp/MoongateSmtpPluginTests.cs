using DryIoc;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Interfaces.Notifications;
using Moongate.Smtp.Plugin;
using SquidStd.Core.Config;
using SquidStd.Core.Directories;
using SquidStd.Plugin.Abstractions.Data;

namespace Moongate.Tests.Smtp;

public sealed class MoongateSmtpPluginTests
{
    [Fact]
    public void Metadata_IdentifiesThePlugin()
        => Assert.Equal("moongate.smtp.plugin", new MoongateSmtpPlugin().Metadata.Id);

    [Fact]
    public void Configure_WithoutHost_RegistersNoChannel()
    {
        // Registering an unconfigured channel would burn the pipeline's retries connecting to nothing.
        var container = Configured();

        Assert.Empty(container.ResolveMany<INotificationChannel>());
    }

    [Fact]
    public void Configure_WhenConfigured_RegistersTheEmailChannel()
    {
        var container = Configured(host: "localhost", from: "shard@example.com");

        Assert.Equal("email", Assert.Single(container.ResolveMany<INotificationChannel>()).Id);
    }

    /// <summary>A container carrying what the real bootstrap supplies around the plugin.</summary>
    private static IContainer Configured(string host = "", string from = "")
    {
        var root = Path.Combine(Path.GetTempPath(), "mg-smtp-test-" + Guid.NewGuid().ToString("N"));

        // Resolve the directory through DirectoriesConfig itself, so the file lands exactly where the
        // plugin will look — the same reason ConsoleConfigFileTests does it this way rather than
        // assuming how the path is transformed.
        var directories = new DirectoriesConfig(root, ["plugins/configs"]);

        // The section key matters: RegisterConfigFile binds the "smtp" section of smtp.yaml, not its root.
        File.WriteAllText(
            Path.Combine(directories["plugins/configs"], "smtp.yaml"),
            $"smtp:\n  Host: '{host}'\n  FromAddress: '{from}'\n  Port: 2525\n  Security: None\n"
        );

        var container = new Container();
        container.RegisterInstance(SquidStdConfig.Load("moongate", root));
        container.RegisterInstance(directories);
        container.RegisterInstance(new MoongateConfig { ShardName = "Britannia", UltimaDirectory = "/tmp" });

        new MoongateSmtpPlugin().Configure(container, new PluginContext());

        return container;
    }
}
