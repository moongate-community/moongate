# Seeing which plugins are active

`GET /api/v1/admin/plugins` reports every plugin the shard activated, each with the HTTP routes it
declares. It requires a staff token: administrators and grand masters only.

```json
[
  {
    "id": "moongate.news.plugin",
    "name": "Moongate News",
    "version": "0.4.0",
    "author": "squid",
    "description": "Shard news",
    "assembly": "Moongate.News.Plugin",
    "isExternal": false,
    "routes": [
      { "method": "GET", "path": "/api/v1/news", "policy": null },
      { "method": "POST", "path": "/api/v1/admin/news", "policy": "admin" }
    ]
  }
]
```

## Reading the response

`isExternal` says where the plugin came from. `false` means it was compiled into the server and
registered in `Program.cs`. `true` means it was loaded from the `plugins` directory at startup — a DLL
dropped in rather than built in.

`policy` names the authorization policy guarding a route, and is `null` when the route is open to
anonymous callers.

`assembly` is how a route is tied to a plugin: each mapped route records the handler that serves it, and
the assembly declaring that handler identifies the plugin. Nothing has to be declared for this to work,
which is why adding an endpoint needs no extra registration to show up here.

## The `moongate.host` entry

The last entry is synthetic. It collects routes belonging to no plugin — the API reference served by
Scalar, and anything the framework maps for itself. It exists so that everything the shard serves appears
somewhere: a route that is live but absent from this list would be worse than an extra line.

Note that `/health` is **not** in it. That route is declared inside the HTTP plugin, so it is reported
against `moongate.http.plugin` along with the rest of that plugin's surface.

## What the plugin list is not

Attribution works at assembly granularity, and several plugins share one assembly: the plugin classes
that live inside the server — script modules, data loaders, commands, packet handlers, event
subscribers — all report `Moongate.Server`.

None of them declares HTTP routes today, so each shows an empty list and nothing is ambiguous. Were
`Moongate.Server` ever to declare routes directly, every plugin sharing that assembly would report the
same full list, because nothing in the routing table says which of them owns a given route. Endpoints
belong in a plugin of their own, which is what keeps this honest.
