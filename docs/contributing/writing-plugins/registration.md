# Registration reference

Every seam a plugin's `Configure(IContainer container, PluginContext context)` can call, at a
glance. The extension methods live in the assembly named in each row — reference it (with
`<Private>false</Private>`, as in [your first plugin](first-plugin.md)) to use the seam.

| Seam | Extension method | Assembly | Registers |
|---|---|---|---|
| [Config section](first-plugin.md) | `RegisterConfigSection<T>("section")` | `SquidStd.Abstractions` | a YAML-bound config POCO (a section of `moongate.yaml`) |
| [Config file](#per-plugin-config-file) | `RegisterConfigFile<T>("section", directory)` | `SquidStd.Abstractions` | a config POCO bound from the plugin's **own** YAML file |
| [Hosted service](hosted-service.md) | `RegisterStdService<TImpl,TImpl>()` | `SquidStd.Abstractions` | an `ISquidStdService` (start/stop lifecycle) |
| [Command](commands.md) | `RegisterCommand<T>(name, minLevel, desc, sources)` | `Moongate.Server.Abstractions` | an `ICommand` |
| [Packet handler](packet-handlers.md) | `RegisterPacketHandler<T>()` | `Moongate.Server.Abstractions` | an `IPacketHandlerRegistration` |
| [Event subscriber](event-subscribers.md) | `RegisterEventSubscriber<T>()` | `Moongate.Server.Abstractions` | an `IEventSubscriberRegistration` |
| [Data loader](data-loaders.md) | `RegisterDataLoader<T>(priority)` | `Moongate.Server.Abstractions` | an `IDataLoader` |
| [REST endpoint](../../under-the-hood/rest-api.md) | `RegisterApiEndpoint<T>()` | `Moongate.Http.Plugin` | an HTTP endpoint group |
| Plain service | `container.Register<I,Impl>(Reuse.Singleton)` | `DryIoc` | any DI dependency |

The last row is plain DryIoc: register any service your plugin needs internally, then inject it
into the components above.

## Per-plugin config file

`RegisterConfigSection<T>("x")` binds a section of the shared `moongate.yaml`. To give a plugin its
**own** config file instead, use `RegisterConfigFile<T>("x", directory)`: the section binds from
`<directory>/x.yaml`, which is generated with defaults at startup and saved/reloaded alongside the
main file. Resolve the directory from `DirectoriesConfig` (registered for you):

```csharp
var directories = container.Resolve<DirectoriesConfig>();
container.RegisterConfigFile<MyPluginConfig>("myplugin", directories["plugins/configs"]);
// → moongate_root/plugins/configs/myplugin.yaml (section "myplugin")
```

This suits a drop-in plugin that ships its own config next to its DLL, and keeps `moongate.yaml`
focused on the core. The [admin console](../../under-the-hood/admin-console.md) uses it; embedded
plugins keep their section in `moongate.yaml`.

New to plugins? Start with [the overview](index.md).
