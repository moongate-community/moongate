# Current limitations

These boundaries are established by the current server code and tests.

## Client-data readiness is not fully validated

The Ultima file-loader startup check points the file locator at `UltimaDirectory` and counts non-empty resolved paths. Its success log is not a full validation of every asset a client session may need, and Moongate does not ship the proprietary client data.

## Data loading stops at the first failure

Registered data loaders run sequentially. A loader exception propagates immediately; the service has no skip, retry, or continue path for the remaining loaders.

## Network configuration accepts IP literals, not hostnames

The listener parses `Network.Address` with `IPAddress.Parse`. Login and redirect handlers separately parse `Network.PublicAddress` the same way. DNS hostnames are therefore not accepted by these paths.

## Not every packet opcode is implemented

When no handler is registered for an opcode, the network service logs `No handler for packet 0x{OpCode:X2} ({Name}) from session {SessionId} - not implemented yet` and does not dispatch the frame.

## Shutdown does not explicitly save a final snapshot

The network lifecycle stops and disposes the listener, while file-loader and data-loader shutdown methods are no-ops. Snapshot writes occur in the initial persistence seeder and the 300-second timer; the current shutdown path does not explicitly invoke one.

Return to [Operations](./operations.md) or [Troubleshooting](./troubleshooting.md).
