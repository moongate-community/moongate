using DryIoc;
using Moongate.Console.Admin.Plugin.Data.Config;
using Moongate.Console.Admin.Plugin.Services.Hosting;
using SquidStd.Abstractions.Extensions.Config;
using SquidStd.Abstractions.Extensions.Services;
using SquidStd.Core.Directories;
using SquidStd.Core.Utils;
using SquidStd.Plugin.Abstractions.Data;
using SquidStd.Plugin.Abstractions.Interfaces.Plugins;

namespace Moongate.Console.Admin.Plugin;

/// <summary>Registers the line-based admin console: its config section and the server that runs it.</summary>
public class MoongateConsolePlugin : ISquidStdPlugin
{
    public PluginMetadata Metadata
        => new()
        {
            Id = "moongate.console.admin.plugin",
            Version = new(VersionUtils.GetVersion(typeof(MoongateConsolePlugin).Assembly)),
            Author = "squid",
            Name = "Moongate Console Admin",
            Description = "Line-based TCP admin console over ICommandService"
        };

    public void Configure(IContainer container, PluginContext context)
    {
        // Per-plugin external config: moongate_root/plugins/configs/console.yaml, generated with
        // defaults at startup. Embedded plugins keep their section in moongate.yaml.
        var directories = container.Resolve<DirectoriesConfig>();
        container.RegisterConfigFile<MoongateConsoleConfig>("console", directories["plugins/configs"]);
        container.RegisterStdService<ConsoleServerService, ConsoleServerService>();
    }
}
