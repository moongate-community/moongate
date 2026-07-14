# Troubleshooting

Each entry below pairs an observable signal with a code-backed cause and a safe corrective action. Placeholders in braces are copied from the logging templates.

## Ultima directory is empty or unusable

**Signal:** startup raises `UODirectoryNotValidException` with `UltimaDirectory is not set in the config; clients will not be able to connect.` when the processed setting is empty.

**Cause:** no non-empty `UltimaDirectory` reached service registration. Normally, `--uo-directory` supplies it or startup assigns the resolved form of `~/uo`.

**Action:** pass `--uo-directory /absolute/path/to/ultima-client` and make sure that path contains client data you are entitled to use.

**Signal:** `UO client files located in {Directory} ({FileCount} files)` renders an unexpected directory or a suspiciously low count.

**Cause:** the file-loader log reports the configured directory and counts non-empty paths known to the Ultima file locator; it does not perform a complete asset-integrity check.

**Action:** verify the rendered directory, correct `--uo-directory`, and restart. Do not interpret this log alone as proof that all required client files exist.

## Listener does not start

**Signal:** `Network service listening on {Address}:{Port}` never appears and startup reports an IP-parse or socket-bind failure. After the transport is running, its exception callback uses the exact wrapper message `Network exception`.

**Cause:** `Network.Address` is parsed as an IP literal immediately before binding. A malformed literal fails parsing; an address not present on the host or an occupied/unavailable `Network.Port` can prevent the TCP server from starting. `Network exception` is the exact wrapper message used for transport exceptions after the server is wired.

**Action:** set `Network.Address` to a local IP literal, check that `Network.Port` is available, and restart. Use `0.0.0.0` only when listening on all IPv4 interfaces is intended.

## Clients cannot follow the server redirect

**Signal:** the listener starts, but a remote client fails after login or server selection. A related handler failure is logged as `Handler for 0x{OpCode:X2} threw for session {SessionId}`.

**Cause:** login responses parse `Network.PublicAddress` as an IP literal and advertise it to the client. The redirect advertises that address with `Network.Port`. A malformed value throws when the account-login or select-server handler builds its response; a loopback or otherwise unreachable value sends the client to the wrong destination.

**Action:** set `Network.PublicAddress` to an IP literal reachable by the client and ensure `Network.Port` reaches the Moongate listener through any host or network firewall. Do not put a hostname in either address setting.

## Seed handshake is rejected

**Signal:** `Rejecting session {SessionId}: malformed seed handshake.` followed by connection closure.

**Cause:** a session awaiting its seed received a malformed raw handshake. The verified rejection case is a zero raw seed; valid paths include a login-seed packet beginning with `0xEF` or a non-zero four-byte raw seed.

**Action:** confirm that the client speaks the expected Ultima login/game protocol and is connecting directly to the configured Moongate port. Repeated signals from unrelated traffic can be treated as protocol mismatch rather than an account failure.

## Packet has no handler

**Signal:** `No handler for packet 0x{OpCode:X2} ({Name}) from session {SessionId} - not implemented yet`.

**Cause:** the received opcode has no registered packet handler. Moongate logs the packet name when known (or `Unknown`) and drops that frame without dispatching it.

**Action:** capture the rendered opcode and name, then compare the client action with Moongate's implemented packet handlers. There is no operator setting that enables an absent handler.

## Startup data loading fails

**Signal:** startup reports an exception from a data loader and never reaches `Executed {Count} data loader(s)`.

**Cause:** data loaders execute sequentially in registered order. An exception is propagated immediately, so later loaders do not run and the completion message is not emitted.

**Action:** use the exception's file/path and validation detail to correct the referenced root data. Restart so the complete loader sequence runs again; do not assume later data was loaded during the failed attempt.

## Snapshot completion is not logged

**Signal:** `Start saving snapshot...` appears without the matching `Snapshot saved in {ElapsedMilliseconds} milliseconds.`

**Cause:** the completion log is emitted only after `SaveSnapshotAsync()` returns. The timer callback contains no local recovery or retry behavior.

**Action:** retain the exception and persistence-service logs from that invocation and inspect the configured root's `saves` path and filesystem access. The current server code does not define an operator retry command, so do not claim a successful snapshot without the completion signal.

Review [Operations](./operations.md), [Current limitations](./limitations.md), or return to the [Server Guide](./index.md).
