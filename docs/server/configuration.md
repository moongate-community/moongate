# Configuration

The server resolves a root directory and eagerly loads its `moongate` configuration before services start. An isolated local launch verified that a new root contains `moongate.yaml` with `logger` and `moongate` sections. The server settings below are under `moongate`.

## Root and Ultima directories

| Input | Default or behavior |
| --- | --- |
| `--root-directory <string>` | Selects the root directory. |
| `MOONGATE_ROOT` | Used when `--root-directory` is absent. |
| Default root | `moongate_root` beneath the process's current working directory when neither CLI nor environment input is set. |
| `--uo-directory <string?>` | Overrides `UltimaDirectory` before services consume the configuration. |
| Default Ultima directory | The resolved form of `~/uo` when `--uo-directory` is absent. This value is assigned to `UltimaDirectory` after the YAML is loaded. |

Both directory inputs pass through path and environment-variable expansion. Startup checks that the resulting `UltimaDirectory` is non-empty; the file-loader service then sets that directory as the Ultima asset root. Moongate does not provide the client files expected there.

## Server settings

The generated file observed in an isolated local launch has this shape:

```yaml
moongate:
  ShardName: Moongate
  UltimaDirectory: /resolved/path/to/uo
  Network:
    Address: 0.0.0.0
    Port: 2593
    PublicAddress: 127.0.0.1
```

| Setting | Code default | Meaning |
| --- | --- | --- |
| `ShardName` | `Moongate` | Name placed in the server list returned after account login. |
| `UltimaDirectory` | No class-level default | Ultima client-data root; startup assigns the CLI value or resolved `~/uo`. |
| `Network.Address` | `0.0.0.0` | Local IP address parsed for the TCP listener bind. |
| `Network.Port` | `2593` | TCP port used by the listener and the game-server redirect. |
| `Network.PublicAddress` | `127.0.0.1` | IP address sent to clients in both the server list and game-server redirect. |

`Address` and `PublicAddress` serve different sides of a connection. `Address` decides which local interfaces accept traffic. `PublicAddress` must be an address clients can reach; it is advertised in protocol responses. Both are parsed as IP address literals rather than resolved as hostnames. An invalid `Address` fails when the listener starts. An invalid `PublicAddress` is not parsed at startup; it fails when an account-login or server-selection handler constructs the relevant response.

Continue to [First launch](./first-launch.md).
