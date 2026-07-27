# Data loaders

A **data loader** reads a file into memory once, at startup. Moongate loads all of its world
data this way — skills, professions, regions, item and mobile templates — and a plugin can add
its own loader for its own files.

## The interface

```csharp
public interface IDataLoader
{
    ValueTask LoadAsync(CancellationToken ct = default);
}
```

`LoadAsync` runs once during startup. A loader typically resolves a file under the server root,
parses it, and hands the result to a registry service the rest of the plugin reads from.

## Locating and parsing the file

Inject `DirectoriesConfig` to resolve a path under the server root, and use `YamlUtils` to
parse — both from `SquidStd.Core`. `RegisterDirectory` returns the absolute path and creates the
directory if it is missing, so the file simply sits under `<root>/plugins/myshard/`. This is the
same shape as the built-in `SkillLoader`.

```csharp
using Moongate.Server.Abstractions.Interfaces.Loading;
using SquidStd.Core.Directories;
using SquidStd.Core.Yaml;

namespace MyShard.Motd.Loading;

/// <summary>Loads plugins/myshard/motd.yaml (a list of lines) into the MOTD registry.</summary>
public sealed class MotdLoader : IDataLoader
{
    private readonly DirectoriesConfig _directories;
    private readonly MotdRegistry _registry;

    public MotdLoader(DirectoriesConfig directories, MotdRegistry registry)
    {
        _directories = directories;
        _registry = registry;
    }

    public ValueTask LoadAsync(CancellationToken ct = default)
    {
        var directory = _directories.RegisterDirectory("plugins/myshard");
        var path = Path.Combine(directory, "motd.yaml");

        if (!File.Exists(path))
        {
            return ValueTask.CompletedTask;
        }

        var lines = YamlUtils.DeserializeFromFile<List<string>>(path) ?? [];
        _registry.SetLines(lines);

        return ValueTask.CompletedTask;
    }
}
```

The registry is an ordinary singleton the loader fills and everything else injects:

```csharp
namespace MyShard.Motd;

/// <summary>Holds the message-of-the-day lines loaded at startup.</summary>
public sealed class MotdRegistry
{
    private IReadOnlyList<string> _lines = [];

    public IReadOnlyList<string> Lines => _lines;

    public void SetLines(IReadOnlyList<string> lines)
        => _lines = lines;
}
```

A matching `plugins/myshard/motd.yaml`:

```yaml
- "Welcome to MyShard!"
- "Server resets nightly at 04:00."
```

## Registering and ordering

Register the registry and the loader in your plugin's `Configure`:

```csharp
container.Register<MotdRegistry>(Reuse.Singleton);
container.RegisterDataLoader<MotdLoader>(priority: 1000);
```

Loaders run in **ascending** priority. Moongate's own loaders occupy the low numbers (up to
150), so pick a priority above them — `1000` here — to load once the core world data your file
might reference is already in place.
