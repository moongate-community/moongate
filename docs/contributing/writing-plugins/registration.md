# Registration reference

Every seam a plugin's `Configure(IContainer container, PluginContext context)` can call, at a
glance. The extension methods live in the assembly named in each row — reference it (with
`<Private>false</Private>`, as in [your first plugin](first-plugin.md)) to use the seam.

| Seam | Extension method | Assembly | Registers |
|---|---|---|---|
| [Config section](first-plugin.md) | `RegisterConfigSection<T>("section")` | `SquidStd.Abstractions` | a YAML-bound config POCO |
| [Hosted service](hosted-service.md) | `RegisterStdService<TImpl,TImpl>()` | `SquidStd.Abstractions` | an `ISquidStdService` (start/stop lifecycle) |
| [Command](commands.md) | `RegisterCommand<T>(name, minLevel, desc, sources)` | `Moongate.Server.Abstractions` | an `ICommand` |
| [Packet handler](packet-handlers.md) | `RegisterPacketHandler<T>()` | `Moongate.Server.Abstractions` | an `IPacketHandlerRegistration` |
| [Event subscriber](event-subscribers.md) | `RegisterEventSubscriber<T>()` | `Moongate.Server.Abstractions` | an `IEventSubscriberRegistration` |
| [Data loader](data-loaders.md) | `RegisterDataLoader<T>(priority)` | `Moongate.Server.Abstractions` | an `IDataLoader` |
| [REST endpoint](../../under-the-hood/rest-api.md) | `RegisterApiEndpoint<T>()` | `Moongate.Http.Plugin` | an HTTP endpoint group |
| Plain service | `container.Register<I,Impl>(Reuse.Singleton)` | `DryIoc` | any DI dependency |

The last row is plain DryIoc: register any service your plugin needs internally, then inject it
into the components above.

New to plugins? Start with [the overview](index.md).
