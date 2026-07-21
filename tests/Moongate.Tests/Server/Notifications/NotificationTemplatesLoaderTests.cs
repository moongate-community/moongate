using Moongate.Server.Loaders;
using Moongate.Server.Services.Notifications;
using SquidStd.Core.Directories;

namespace Moongate.Tests.Server.Notifications;

public sealed class NotificationTemplatesLoaderTests
{
    [Fact]
    public async Task LoadAsync_WhenMissing_SeedsTheDefaultsAndRegistersThem()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var templates = new NotificationTemplateService();
        var loader = new NotificationTemplatesLoader(templates, directories);

        try
        {
            await loader.LoadAsync();

            var templatesDirectory = Path.Combine(directories.GetPath("notification"), "templates");

            // Seeded as directories-per-channel, with the dot-free id intact.
            Assert.True(File.Exists(Path.Combine(templatesDirectory, "log", "account_verification.mgtmpl")));
            Assert.True(File.Exists(Path.Combine(templatesDirectory, "email", "account_verification.mgtmpl")));

            Assert.Equal(2, templates.Count);
            Assert.NotNull(
                templates.Render("log", "account_verification", new { Username = "tom", Email = "t@x", Token = "abc" })
            );
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_RegistersByDirectoryNameAndFileName()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var discord = Path.Combine(directories.RegisterDirectory("notification"), "templates", "discord");
        Directory.CreateDirectory(discord);
        File.WriteAllText(Path.Combine(discord, "shard_online.mgtmpl"), "{{ shard_name }} is up");

        var templates = new NotificationTemplateService();
        var loader = new NotificationTemplatesLoader(templates, directories);

        try
        {
            await loader.LoadAsync();

            // The directory is the channel id and the file name is the template id — no registry, no
            // configuration: a plugin channel ships its own directory and is picked up.
            var content = templates.Render("discord", "shard_online", new { ShardName = "Britannia" });
            Assert.Equal("Britannia is up", content!.Body.Trim());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LoadAsync_BrokenTemplate_IsReportedAndTheRestStillLoad()
    {
        var root = NewRoot();
        var directories = new DirectoriesConfig(root, Array.Empty<string>());
        var log = Path.Combine(directories.RegisterDirectory("notification"), "templates", "log");
        Directory.CreateDirectory(log);
        File.WriteAllText(Path.Combine(log, "broken.mgtmpl"), "{{ if }}");
        File.WriteAllText(Path.Combine(log, "fine.mgtmpl"), "ok");

        var templates = new NotificationTemplateService();
        var loader = new NotificationTemplatesLoader(templates, directories);

        try
        {
            // One bad file must not stop the shard from booting, nor hide the good templates.
            await loader.LoadAsync();

            Assert.Equal(1, templates.Count);
            Assert.NotNull(templates.Render("log", "fine", new { }));
            Assert.Null(templates.Render("log", "broken", new { }));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string NewRoot()
        => Path.Combine(Path.GetTempPath(), "mg-notification-tests-" + Guid.NewGuid().ToString("N"));
}
