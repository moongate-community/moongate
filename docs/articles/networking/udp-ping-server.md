# UDP Ping Server

Moongate runs a small UDP echo listener for operational ping checks. It is not part of the game protocol; it exists so operators and tooling can confirm that the shard is reachable over UDP.

## Configuration

Configure it in `moongate.json` under `game`:

```json
{
  "game": {
    "pingServerEnabled": true,
    "pingServerPort": 12000
  }
}
```

Defaults:

- `game.pingServerEnabled`: `true`
- `game.pingServerPort`: `12000`

## Runtime Behavior

When the network service starts, it opens one UDP listener per local interface on the configured port.

- Each received datagram is echoed back unchanged to the sender.
- The server does not parse or transform payloads.
- If a local bind fails on one interface, Moongate logs a warning and continues with the others.
- When the network service stops, the UDP listeners are shut down with the rest of the network lifecycle.

## Validation

A simple outside-in check is to send a UDP payload and verify that the same bytes come back. For example:

```bash
python - <<'PY'
import socket

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.settimeout(2)
sock.sendto(b'ping', ('127.0.0.1', 12000))
data, addr = sock.recvfrom(1024)
print(data.decode('ascii'), addr)
PY
```

Expected result: the command prints `ping`, and the reply comes from the same UDP port you configured.

## Related Docs

- [Configuration Guide](../getting-started/configuration.md)
- [Protocol Reference](protocol.md)
