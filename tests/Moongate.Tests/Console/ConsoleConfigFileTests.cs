using DryIoc;
using Moongate.Console.Admin.Plugin;
using Moongate.Console.Admin.Plugin.Data.Config;
using SquidStd.Core.Config;
using SquidStd.Core.Directories;
using SquidStd.Plugin.Abstractions.Data;

namespace Moongate.Tests.Console;

public class ConsoleConfigFileTests
{
    [Fact]
    public void Configure_BindsConsoleFromPluginsConfigsFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "mg-console-cfg-" + Guid.NewGuid().ToString("N"));

        try
        {
            // Use DirectoriesConfig itself to resolve (and create) the dir, so the file lands exactly
            // where the plugin will look — no assumption about the snake-case path transform.
            var directories = new DirectoriesConfig(root, ["plugins/configs"]);
            File.WriteAllText(Path.Combine(directories["plugins/configs"], "console.yaml"), "console:\n  Enabled: true\n  Port: 9999\n");

            using var container = new Container();
            container.RegisterInstance(SquidStdConfig.Load("moongate", root));
            container.RegisterInstance(directories);

            new MoongateConsolePlugin().Configure(container, new PluginContext());

            var config = container.Resolve<MoongateConsoleConfig>();
            Assert.True(config.Enabled);
            Assert.Equal(9999, config.Port);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
