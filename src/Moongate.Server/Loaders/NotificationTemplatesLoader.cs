using Moongate.Server.Abstractions.Interfaces.Loading;
using Moongate.Server.Abstractions.Interfaces.Notifications;
using Moongate.Server.Internal;
using Serilog;
using SquidStd.Core.Directories;

namespace Moongate.Server.Loaders;

/// <summary>
/// Seeds and loads notification templates. The directory name is the channel id and the file name is
/// the template id, so adding a channel is adding a directory — there is no registry to keep in sync.
/// </summary>
public sealed class NotificationTemplatesLoader : IDataLoader
{
    private const string EmbeddedDirectory = "Assets/Notification/Templates";
    private const string EmbeddedNamespace = "Moongate.Server.Assets.Notification.Templates";
    private const string Extension = "*.mgtmpl";

    private readonly ILogger _logger = Log.ForContext<NotificationTemplatesLoader>();
    private readonly INotificationTemplateService _templates;
    private readonly DirectoriesConfig _directories;

    public NotificationTemplatesLoader(INotificationTemplateService templates, DirectoriesConfig directories)
    {
        _templates = templates;
        _directories = directories;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var notificationDirectory = _directories.RegisterDirectory("notification");
        var templatesDirectory = Path.Combine(notificationDirectory, "templates");

        if (!Directory.Exists(templatesDirectory))
        {
            EmbeddedResourceDirectorySeeder.SeedAtomic(
                typeof(NotificationTemplatesLoader).Assembly,
                EmbeddedDirectory,
                EmbeddedNamespace,
                templatesDirectory
            );

            _logger.Information("Seeded default notification templates at {Path}", templatesDirectory);
        }

        if (!Directory.Exists(templatesDirectory))
        {
            return ValueTask.CompletedTask;
        }

        var loaded = 0;
        var failed = 0;

        foreach (var channelDirectory in Directory.GetDirectories(templatesDirectory)
                                                  .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var channelId = Path.GetFileName(channelDirectory);

            foreach (var file in Directory.GetFiles(channelDirectory, Extension, SearchOption.TopDirectoryOnly)
                                          .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var templateId = Path.GetFileNameWithoutExtension(file);

                try
                {
                    _templates.Register(channelId, templateId, File.ReadAllText(file));
                    loaded++;
                }
                catch (Exception exception)
                {
                    // One broken template must not stop the shard from booting, but it must be loud: this
                    // is the moment the operator can still fix it.
                    failed++;
                    _logger.Error(exception, "Skipping notification template {Path}", file);
                }
            }
        }

        _logger.Information(
            "Loaded {Count} notification template(s) from {Path} ({Failed} skipped)",
            loaded,
            templatesDirectory,
            failed
        );

        return ValueTask.CompletedTask;
    }
}
