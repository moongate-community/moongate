# Writing plugins

A **plugin** is a .NET assembly that extends a Moongate server in C#. You build it as a
class library, drop the resulting `.dll` into the server's `plugins/` directory, and the
server discovers and loads it at startup — no fork, no changes to the server itself.

Everything the server ships — the HTTP API, the admin console, the command set, the packet
handlers — is registered through the same plugin mechanism you use here.

## Plugin or Lua script?

Moongate has two extension paths. Reach for a **plugin** when you need to add code that runs
inside the server process:

- a background **service** (a socket, a timer, a bridge to another system);
- a **command** (`.something`) for GMs or the admin console;
- a **packet handler** for an inbound client packet;
- an **event subscriber** that reacts to something happening in the world;
- a **data loader** that reads your own files at startup;
- a **REST endpoint**.

Reach for [Lua scripting](../../scripting/index.md) instead when you are writing game-content
logic — item and mobile behaviour, loot, world events — and want to iterate live with no
compile step. Scripting needs no C# at all.

## Anatomy of a plugin

A plugin is one class implementing `ISquidStdPlugin`:

```csharp
public interface ISquidStdPlugin
{
    PluginMetadata Metadata { get; }
    void Configure(IContainer container, PluginContext context);
}
```

- **`Metadata`** is the plugin's identity — a stable id, name, version, author, and an
  optional list of other plugin ids that must load first.
- **`Configure`** registers everything the plugin contributes into the DryIoc `container`.
  It runs once, during startup, before the server begins — so config sections and services
  registered here are wired up in time. See [the architecture overview](../../under-the-hood/architecture.md)
  for the container and startup sequence.

## How the server loads a plugin

The server calls `builder.FromDirectory("plugins")` at startup. The loader then:

- scans the `plugins/` directory (relative to the server root) for `*.dll`, **non-recursively**;
- loads each into the **default assembly load context** and instantiates **every concrete
  `ISquidStdPlugin`** it finds — so your plugin class needs a **public parameterless constructor**;
- orders all plugins so each loads after the ids in its `Metadata.Dependencies`.

Two consequences worth stating plainly:

- **There is no version isolation.** Plugins share assembly identity with the host, so you
  must build against the *same* Moongate and SquidStd assemblies the server runs. The
  [first-plugin walkthrough](first-plugin.md) shows how to reference them without copying
  them into `plugins/`.
- **Plugins are fully trusted, and any failure aborts startup.** A duplicate id, a missing
  dependency, a dependency cycle, or an exception thrown from `Configure` stops the server
  from booting. An *optional* plugin should therefore catch and log its own runtime failures
  rather than let them escape — see [hosted services](hosted-service.md).

## Next

- [Your first plugin](first-plugin.md) — build, deploy and run one end to end.
- [Registration reference](registration.md) — every seam `Configure` can call, in one table.
