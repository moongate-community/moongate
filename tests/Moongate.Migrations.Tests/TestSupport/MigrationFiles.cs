namespace Moongate.Migrations.Tests.TestSupport;

public sealed class MigrationFiles : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), $"moongate_migrations_{Guid.NewGuid():N}");
    public string Core => Path.Combine(Root, "migrations");
    public string Plugins => Path.Combine(Root, "plugins");

    public MigrationFiles()
    {
        Directory.CreateDirectory(Path.Combine(Core, "auth"));
        Directory.CreateDirectory(Path.Combine(Core, "world"));
        Directory.CreateDirectory(Plugins);
    }

    public void Write(string relative, string content)
    {
        var path = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
        => Directory.Delete(Root, true);
}
