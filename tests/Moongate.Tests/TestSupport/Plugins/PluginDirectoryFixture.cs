using Moongate.Core.Directories;

namespace Moongate.Tests.TestSupport.Plugins;

public sealed class PluginDirectoryFixture : IDisposable
{
    public DirectoriesConfig Directories { get; }

    public PluginDirectoryFixture(params string[] directories)
    {
        var root = Path.Combine(Path.GetTempPath(), $"moongate-plugin-loader-{Guid.NewGuid():N}");
        Directories = new DirectoriesConfig(root, directories.Length == 0 ? ["plugins"] : directories);
    }

    public string Deploy(string name, string? directoryName = null)
    {
        directoryName ??= name;
        var source = Path.Combine(AppContext.BaseDirectory, "PluginFixtures", name);
        var destination = Path.Combine(Directories["plugins"], directoryName);
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }

        if (directoryName != name)
        {
            File.Copy(Path.Combine(destination, name + ".dll"), Path.Combine(destination, directoryName + ".dll"));
            File.Copy(Path.Combine(destination, name + ".deps.json"), Path.Combine(destination, directoryName + ".deps.json"));
        }

        return destination;
    }

    public void Dispose()
    {
        Directory.Delete(Directories.Root, true);
    }
}
