# Admin console

Moongate can expose a small line-based **admin console** over TCP: connect,
log in with a staff account, and run the server's commands. It is served by
`Moongate.Console.Admin.Plugin`, one of the built-in plugins, and like the REST
API it is genuinely optional — the game server owns the listener, so removing
the plugin removes the whole console.

It is **disabled by default** and it is **plaintext** (no SSH, no TLS): only
turn it on where the network already protects it — loopback, a private LAN, or
a Tailscale interface.

## Turning it on

The console has its **own** config file, `moongate_root/plugins/configs/console.yaml`, generated with
defaults on first start (it does not live in `moongate.yaml`). Set its `console:` section:

```yaml
console:
  Enabled: true          # opt-in; false (default) never binds
  Address: 127.0.0.1     # keep off public interfaces — traffic is unencrypted
  Port: 4050
  MaxSessions: 4         # concurrent connections before new ones are refused
```

| Key | Type | Default | Meaning |
|---|---|---|---|
| `Enabled` | bool | `false` | When false the console never binds. |
| `Address` | string | `127.0.0.1` | Bind address. Plaintext — keep it off public NICs. |
| `Port` | int | `4050` | Listen port. |
| `MaxSessions` | int | `4` | Maximum concurrent sessions; further connections are refused. |

On start the server logs `Admin console listening on 127.0.0.1:4050`. A bind
failure (port in use, bad address) is logged and swallowed — the console
becomes unavailable but the game server keeps running.

## Connecting

Use a raw line client such as `nc` or `socat` (not the `ssh` command — this is
not SSH):

```text
$ nc 127.0.0.1 4050
Moongate admin console.
login:
gm
password:
secret
Authenticated. Type 'help' or 'quit'.
> broadcast Server restarting in 5 minutes
Broadcast sent.
> help
  broadcast - Sends a server-wide system message.
  help - list commands
  quit - close the session
> quit
Bye.
```

Login checks the username and password against the account store and requires
**GrandMaster or Administrator** level; anything less is refused. Wrong
credentials give a uniform `Login failed.` (three attempts, then the connection
closes). An unknown command — or one you are not allowed to run — gives the
same `Unknown command.` for every case, so the console never reveals which
commands exist.

Commands run on the game loop, exactly as an in-game `.` command does, so they
see consistent world state.

## Exposing a command to the console

The console runs the *same* commands as the in-game `.` dispatcher — a command
is reachable from the console only if its registration lists the `Console`
source. Add it to the `Sources` flags when registering:

```csharp
container.RegisterCommand<BroadcastCommand>(
    "broadcast|bc",
    AccountLevelType.GrandMaster,
    "Sends a server-wide system message.",
    CommandSourceType.InGame | CommandSourceType.Console);
```

A command that needs an in-world actor should stay `InGame`-only: console
invocations carry no `MobileEntity` (`CommandContext.Actor` is null).
