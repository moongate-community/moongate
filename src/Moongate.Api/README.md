# Moongate.Api

Standalone, typed request/reply channels over TCP and mutual TLS for .NET 10. The library uses `Moongate.Network` for transport and MessagePack for portable binary payloads. It has no dependency on the game server, GameLoop, UO packet registry, HTTP, or the process-local event bus.

## Install and register a contract

```shell
dotnet add package Moongate.Api
dotnet add package MessagePack
```

Use MessagePack 3.1.9 with this version. Its analyzer generates serializers for your own DTOs. Every request has a stable, nonzero `ushort` operation ID and one response type. Contracts use explicit integer keys; append fields without renumbering existing keys. Do not send CLR type names or typeless payloads.

`Program.cs`:

<!-- nuget-smoke:Program.cs -->
```csharp
using Moongate.Api.Registry;

var registry = new ApiRegistry();
registry.RegisterContract<IncrementRequest, IncrementResponse>();
registry.Freeze();
Console.WriteLine($"API contracts: {registry.ContractCount}");
```

`IncrementRequest.cs`:

<!-- nuget-smoke:IncrementRequest.cs -->
```csharp
using MessagePack;
using Moongate.Api.Attributes;
using Moongate.Api.Interfaces.Contracts;

[ApiOperation(100), MessagePackObject]
public sealed partial class IncrementRequest : IApiRequest<IncrementResponse>
{
    [Key(0)] public int Value { get; init; }
}
```

`IncrementResponse.cs`:

<!-- nuget-smoke:IncrementResponse.cs -->
```csharp
using MessagePack;

[MessagePackObject]
public sealed partial class IncrementResponse
{
    [Key(0)] public int Value { get; init; }
}
```

The output is `API contracts: 1`. This example needs no listener or credentials.

## Handle requests and open a channel

Implement the typed handler in its own `IncrementHandler.cs`:

```csharp
using Moongate.Api.Data.Requests;
using Moongate.Api.Interfaces.Handlers;

public sealed class IncrementHandler : IApiHandler<IncrementRequest, IncrementResponse>
{
    public ValueTask<IncrementResponse> HandleAsync(
        ApiRequestContext context, IncrementRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new IncrementResponse { Value = request.Value + 1 });
    }
}
```

Provide `serverTls` and `clientTls` as `ApiTlsOptions` from your application's credential provider, then compose the endpoints:

```csharp
using System.Net;
using Moongate.Api.Client;
using Moongate.Api.Data.Config;
using Moongate.Api.Registry;
using Moongate.Api.Server;

var serverRegistry = new ApiRegistry();
serverRegistry.RegisterHandler(() => new IncrementHandler());
await using var server = new ApiServer(
    new IPEndPoint(IPAddress.Loopback, 0), serverRegistry,
    new ApiOptions(), serverTls, TimeProvider.System);
await server.StartAsync();

var clientRegistry = new ApiRegistry();
clientRegistry.RegisterContract<IncrementRequest, IncrementResponse>();
await using var client = new ApiClient(
    clientRegistry, new ApiOptions(), clientTls, TimeProvider.System);
var connection = await client.ConnectAsync(server.Endpoint!, "game.internal", "realm-a");
var response = await connection.RequestAsync<IncrementRequest, IncrementResponse>(
    new IncrementRequest { Value = 41 });
Console.WriteLine(response.Value); // 42
```

The server certificate must match `game.internal`, and the client allowlist must map that exact certificate to `realm-a`. The server's allowlist must grant operation 100 to the client's certificate identity. Constructor validation and registry freezing happen before bind/connect. Factories resolve once at `Freeze()`; registration after freezing fails.

Both sides can initiate requests on the same `IApiConnection`. Register a handler on each receiving endpoint and grant the corresponding peer permission. `ApiServer.Connections` returns an immutable snapshot. In Moongate.Server, `container.RegisterApiHandler<IncrementHandler>()` infers the contract and owns a singleton handler; that adapter is outside this standalone library and starts no listener.

## TLS identity and ownership

Each `ApiTlsOptions` supplies:

- `Certificate`: the local leaf certificate with its private key.
- `TrustedRoots`: the private CA roots trusted exclusively by this endpoint.
- `PeersByCertificateSha256`: hexadecimal SHA-256 leaf fingerprints mapped to immutable `ApiPeerIdentity` objects and explicit operation allowlists.

Both sides validate trust, certificate validity and usage, require an allowed leaf, and reject missing certificates. Clients also validate the server hostname and expected peer ID. TLS versions and cipher selection follow the operating system. Renegotiation and session resumption are disabled.

Authorization is local and precedes body decoding. A game endpoint can authorize an administrative client with the login process offline. Remote payloads never choose their authenticated identity.

This private-network deployment uses removal of a leaf fingerprint as its revocation mechanism. Update the allowlist, restart the endpoint and reconnect to apply removal or rotation. Online CRL/OCSP checks and certificate downloads are disabled; chain, expiry, usage, hostname and allowlist checks remain required. Existing sessions retain their authenticated snapshot until closed.

Server admission reserves capacity before TLS and retains it until failed setup cleanup or actual API completion, including after socket disconnection while a handler remains active.

Endpoints snapshot options, mappings, permissions and certificate handles. Callers may dispose their original certificate objects after construction. Endpoint-owned copies remain alive until setup, I/O and handlers actually finish. Obtain production certificates from your approved credential provider; do not embed private material in source, logs or command arguments.

## Generate a private CA

The repository ships `scripts/api-certificates.sh` (bash and OpenSSL), which produces certificates in exactly the shape the policy validates: a CA restricted to signing, server leaves with the `serverAuth` usage and a DNS name equal to the host clients connect to, client leaves with the `clientAuth` usage, all with `digitalSignature` and two-year validity by default.

```shell
scripts/api-certificates.sh init                       # artifacts/api-certs/ca.crt, ca.key
scripts/api-certificates.sh issue server game.internal # game.internal.crt/.key/.pfx
scripts/api-certificates.sh issue client admin-console # admin-console.crt/.key/.pfx
scripts/api-certificates.sh fingerprint admin-console  # SHA-256 for the allowlist
```

`ca.crt` is what `TrustedRoots` takes on both sides; `ca.key` stays with whoever issues certificates. Each endpoint loads its own leaf as `Certificate`, from the PEM pair with `X509Certificate2.CreateFromPemFile` or from the PKCS#12 file with `X509CertificateLoader.LoadPkcs12FromFile`. The PKCS#12 password is read from `MOONGATE_PFX_PASSWORD` or prompted for; the script never takes it as an argument. `issue` prints the leaf's SHA-256 fingerprint in the form `PeersByCertificateSha256` expects, and `fingerprint` prints it again later. To revoke a peer, remove its fingerprint and restart the endpoint; the script produces no CRL or OCSP material because the policy never consults them. Pass `--out DIR` to write somewhere other than `artifacts/api-certs` and `--days N` to change a validity period.

## Wire format

Every frame begins with a four-byte unsigned big-endian length **excluding the prefix**, followed by one MessagePack array:

```text
[version, kind, requestId, operationId, payloadBin]
```

Version is 1. Kinds are Request=1, Response=2 and Error=3. Request IDs are nonzero unsigned 32-bit values, strictly increasing in wire order per caller and connection. They are never reused: drain and explicitly reconnect when the range is exhausted. Operation IDs are nonzero unsigned 16-bit values.

`payloadBin` contains exactly one typed MessagePack value. Supported portable values are integer-key arrays, integers, booleans, strings, binary data and nil fields. Maps, extensions/native CLR formats, compression, notifications, invalid UTF-8, extra envelope bytes and excessive nesting are rejected. Payload depth is capped at 64. Unknown DTO fields may be appended for forward compatibility; there is no dynamic type resolution from wire input.

For example, Request 42, operation 100, payload `[7]` has these complete frame bytes:

```text
00 00 00 09 95 01 01 2A 64 C4 02 91 07
```

## Bounds, ordering and shutdown

| Scope | Default |
|---|---|
| Frame body | 64 KiB, excluding the four-byte prefix |
| Per connection | 16 pending local calls, 16 queued incoming requests, 32 queued outgoing frames |
| Execution | One active handler per connection; 8 active handlers per endpoint instance |
| Endpoint admission | 32 connections, including setup; 8 concurrent handshakes |
| Deadlines | 5 seconds each for handshake, local call, write and remote handler |
| Shutdown | 10 seconds |

One active incoming handler and one active outgoing write are separate from their queue capacities. `ApiOptions` validates finite positive limits. Frame configuration may not exceed 16 MiB minus the prefix. Accepted frames and queued serialized payloads own their memory.

A single async worker executes admitted handlers in FIFO order on each connection. Connections share the endpoint's execution semaphore. Responses bypass that worker so they can complete pending calls. Concurrent producers receive request IDs when admitted to the send queue; caller invocation order is not promised before serialization completes. Avoid cyclic handler RPC, such as A awaiting B while B awaits another handler on A's same ordered connection. Domain handlers must explicitly marshal game mutations to GameLoop; this library does not do so.

A local call deadline starts when capacity is reserved, including serialization and queued time. Cancellation or timeout removes an unsent queued request and immediately reclaims its capacity. After sending begins, cancellation only ends the local wait. A remote handler deadline begins at arrival, including queue and shared-permit waits. Expiry sends at most one `DeadlineExceeded` response and signals cancellation. A non-cooperative handler retains its slot until it actually exits; late success or failure is observed and cannot produce another reply.

`StopAsync()` closes listener and pending setup, stops admission, and drains admitted work and output. At the shutdown limit it closes I/O and cancels remaining work. It throws `TimeoutException` if ownership has not completed, while continuing to observe unfinished handlers and retaining their resources. `IApiConnection.Completion` ends only after real cleanup. Caller cancellation cancels the stop wait, not the initiated stop. A complete stop allows a fresh server generation; disposal is terminal. `CloseAsync()` requests immediate connection closure and is safe inside a handler; do not await that connection's `Completion` from its own handler.

## Observable outcomes

| Result | Meaning |
|---|---|
| `ApiBusyException` | Local capacity rejected the call before sending |
| `OperationCanceledException` | Caller canceled its wait |
| `TimeoutException` | Local call, preparation, write or shutdown deadline elapsed |
| `IOException` | Transport/protocol failure or disconnection |
| `ApiRemoteException` | Remote endpoint returned one of the error codes below |

Remote codes are UnsupportedOperation=1, Forbidden=2, InvalidRequest=3, Busy=4, Unavailable=5, DeadlineExceeded=6 and InternalError=7. Errors use `[code, message]` with fixed safe messages. Unknown codes, mismatched operation IDs, never-issued response IDs and malformed active responses close the channel. Responses for already completed/canceled assigned IDs are counted and ignored without decoding their DTO. Handler exception details and request payloads are never returned as error messages.

Timeout, cancellation or disconnection **after sending does not prove the operation did not execute**. There is no automatic request retry, replay or reconnect loop. Application code may reconnect explicitly and retry only operations whose domain semantics permit it; correlation IDs are not durable deduplication keys.

## Scope and validation

Tests cover byte-level framing, malicious payload bounds, certificate rejection, authorization, queue/handler limits, cancellation races, lifecycle, and real independent .NET processes. The process tests include a forbidden peer, direct game access with login offline, and a lost response followed by reconnection without replay.

Login/game role composition, realm discovery and leases, account commands, admission tickets, minimum account type, reconnect backoff, HTTP adapters and GameLoop integration belong to the subsequent application phase. They are not started or simulated by registering this library.
