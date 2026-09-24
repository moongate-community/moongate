# Moongate.Server.Admin

Embedded optional gRPC administration plugin distributed with Moongate Server. It hosts the versioned administration
contracts using existing account services and host-owned session infrastructure. It is not a disk plugin or a standalone
NuGet package.

Configure `[admin_api]` in `moongate.toml`; the listener is disabled by default and uses port 2590 with standard server TLS
when enabled. Login and Standalone hosts expose account administration; Game hosts validate shared Redis sessions without
opening the Accounts database.

See the [administration guide](../../docs/admin-api.md) for certificates, initial account access, roles, token revocation,
clients and Docker deployment. Portable clients reference [Moongate.Admin.Contracts](../Moongate.Admin.Contracts/README.md),
not this host implementation.
