# version

Shows the version the server runs, whether it is a Debug or a Release build, and when it was built.

| Syntax | Console | In game | Minimum level | Role |
| --- | --- | --- | --- | --- |
| `version` | Yes | Yes | Regular | Every role |

```text
version
.version
```

For every player, and from the console. It takes no arguments and prints one line:

```text
Moongate 0.14.0 "Lilly" (Release), built 2026-10-05 14:32 UTC.
```

- `0.14.0` is the version of the release and `Lilly` its codename.
- `Release` or `Debug` is the configuration the binaries were built in. A shard open to players
  runs a Release build; a Debug one is what `dotnet run` and a development build give.
- The time is when `mgserver` was built, in UTC: two builds of the same version are told apart by
  it.

The same three facts open the server's console at every start, under the banner:

```text
Version: 0.14.0 (Release) Codename: "Lilly"
        Built: 2026-10-05 14:32 UTC
```

`mgctl init` shows them too, for `mgctl` itself: it is built on its own, so its `Built:` can differ
by a minute from the server's. `mgctl --version` prints the bare number, `0.14.0`, for scripts.

A build that does not say how or when it was made reads `(unknown)` and `built unknown`.

## See also

- [All commands](../commands.md)
- [`uptime`](uptime.md)
