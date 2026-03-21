# Client Encryption

Moongate supports both plain and encrypted Ultima Online clients during the login bootstrap.

## Supported Modes

`moongate.json` under `game` supports:

- `None`
- `Unencrypted`
- `Encrypted`
- `Both`

Recommended default:

- `Both`

This keeps ClassicUO/plain clients working while also accepting clients that use the legacy UO login/game encryption flow.

## Handshake Detection

Moongate follows the same practical model used by ModernUO:

- plain login bootstrap is accepted when the first parsed login packet is already `0x80`
- plain game bootstrap is accepted when the first parsed game-login packet is already `0x91`
- otherwise the server attempts to detect and decrypt:
  - login encryption for the `0x80` account-login bootstrap
  - game encryption for the `0x91` game-login bootstrap

The connection stays plain until the first encrypted bootstrap packet is detected successfully.
After that, the server attaches an encryption middleware to the TCP client and all following inbound/outbound traffic uses the negotiated encryption state.

## Runtime Shape

The implementation is split into three parts:

- `NetworkService`
  - keeps handshake detection and policy enforcement
- `EncryptionMiddleware`
  - decrypts inbound bytes and encrypts outbound bytes once the session has an active cipher
- `GameNetworkSession`
  - stores the negotiated seed, client version, and current `IClientEncryption`

## Algorithms

Moongate supports:

- login encryption
- game encryption

The game-server path intentionally uses the legacy UO MD5-derived XOR state because that is part of the historical client protocol and is required for compatibility.

## Enhanced Client Session Shape

Enhanced-client support stays separate from transport encryption:

- `0xE1` `ClientType` updates the session client capability
- `0xBD` `ClientVersion` can also promote the session into an enhanced-capable mode
- `GameNetworkSession` exposes `ClientType` and `IsEnhancedClient`

Moongate currently uses that session capability to:

- set the KR/UO3D-compatible flags on the `0xA9` character list
- prefer the new mobile-incoming format for enhanced sessions during world sync and movement visibility updates

Encryption and enhanced-client support are intentionally orthogonal. An enhanced-capable client can still connect plain when `encryptionMode` allows it.

## Configuration Example

```json
{
  "game": {
    "encryptionMode": "Both",
    "encryptionDebug": false
  }
}
```

## Notes

- plain clients still work when `encryptionMode` is `Both`
- `LoginEncryption` only decrypts client-to-server login bootstrap traffic
- `GameEncryption` is used for both inbound and outbound traffic after game-login detection
- the login seed (`0xEF`) remains the first plain bootstrap packet and is used to initialize the session seed/client version
