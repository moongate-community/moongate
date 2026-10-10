# Message of the day

Edit `<root>/data/motd.toml` to show a private chat message whenever a character enters the world. The repository ships an example at `moongate_root/data/motd.toml`. Restart the game server to apply changes.

```toml
lines = [
    "Welcome ${player_name} to ${server_name} (${codename} ${version})!",
    "Realm: ${realm_name} — players online: ${users_online}",
]
```

Each nonblank entry becomes one Unicode system speech message to the entering character, in file order. The message is sent after the client receives the enter-world sequence. Other players cannot see it.

## Variables

Variable names are English `snake_case` and use the `${name}` syntax.

| Variable | Value |
| --- | --- |
| `${version}` | Server version, without build metadata |
| `${codename}` | Server release codename |
| `${server_name}` | `shard.shard_name` from `config/moongate.toml` |
| `${realm_name}` | Client-facing name of the current game realm |
| `${player_name}` | Name of the character entering the world |
| `${users_online}` | Connected characters in the world on this game process, including the entrant |

`users_online` does not count login-only sessions, disconnected sessions, characters not yet in the world, or players on another game process.

## Validation and failures

The game role loads and validates the file at startup. A missing file logs one warning and sends no MOTD. Malformed TOML, a missing `lines` array, invalid variable names, unknown variables, an entry that is not a string, an entry with a NUL character, and an entry that cannot fit a Unicode speech line stop startup; errors include the file path and the line index when applicable. Empty or whitespace-only entries are skipped. Text outside a complete `${...}` token is literal.

A plugin resolver that fails, or a line that grows beyond a Unicode speech packet once its variables are replaced, skips that line and logs a warning without recording the rendered text. Later lines are still sent. If the character's original session closes or is replaced, delivery stops.

## Plugin variables

A C# plugin can register variables in `IMoongatePlugin.Register`. Declare a dependency on `com.github.moongate-community.moongate.plugins.ultima` so the Ultima plugin has registered the variable registry first. Reference `Moongate.Server.Ultima` and import `Moongate.Server.Ultima.Extensions`.

```csharp
container.RegisterMotdVariable(
    "season_name",
    (context, cancellationToken) => ValueTask.FromResult("Summer")
);
```

Then use `${season_name}` in `motd.toml`. Names must match `[a-z][a-z0-9_]*`; duplicate names, including built-in names, fail plugin registration. Registration closes before file validation, so variables cannot be added at runtime. Resolver output is inserted as literal text and is never expanded again.
