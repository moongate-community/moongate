# Moongate.Api

Standalone typed request/reply APIs over private TCP connections, with MessagePack framing, mutual TLS and bounded execution. The library reuses Moongate.Network and does not require the server or game world.

Mutual TLS uses custom private CA roots, certificate expiry and usage validation, server hostname validation, and an exact SHA-256 leaf fingerprint allowlist. Identities and operation permissions are local immutable snapshots; authorization does not call a login service.

The private deployment revocation mechanism is removal of a leaf fingerprint from the allowlist, followed by endpoint restart and reconnection. Online CRL/OCSP checks and certificate downloads are disabled; chain, expiry, usage, hostname and allowlist checks remain required. Callers supply certificates from their credential provider. Endpoints own certificate copies until their connections stop.
