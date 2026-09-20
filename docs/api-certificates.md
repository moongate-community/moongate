# API certificates

Moongate's internal API uses MessagePack over TCP with **mutual TLS (mTLS)** on
port **2594** by default. Each endpoint has its own certificate and private key.
Both sides verify the remote certificate against explicit trust roots and a
SHA-256 fingerprint allowlist. Clients also check the server's DNS name or IP
address and expected peer ID.

Moongate.Server can create a missing self-signed identity. This does not trust
other servers automatically or register login/realm operations. The examples
below prepare two identities named `login` and `realm-1`; application handlers
and outbound connections still need to be registered by your code or plugin.
See [API host configuration](server-configuration.md#enable-the-internal-api-server)
and the [typed API client example](../src/Moongate.Api/README.md#handle-requests-and-open-a-channel).

## Generate an identity without opening the API port

Use a build containing certificate generation; older releases do not recognize
these options. Configure the server normally using [First start](getting-started.md),
then replace its `[api]` section in `<root>/config/moongate.toml`:

```toml
[api]
enabled = false
auto_generate_certificate = true
listen_address = "0.0.0.0"
port = 2594
certificate_path = "tls/server.pfx"
certificate_password_environment_variable = ""
certificate_dns_names = ["realm-1"]
certificate_ip_addresses = []
trusted_root_paths = []
peers = []
```

1. Set `certificate_dns_names` to the names clients will pass as the TLS target
   host, such as the Compose service name `realm-1`. For connections using an IP
   target, put that literal in `certificate_ip_addresses`. At least one DNS name
   or IP is required; URLs, wildcard DNS names and scoped IPv6 addresses are not
   accepted. Omitted arrays default to `["localhost"]` and `["127.0.0.1", "::1"]`.
2. Start Moongate with that data root. At API service startup it creates
   `<root>/config/tls/server.pfx` and `<root>/config/tls/server.pfx.pem`. It logs
   the paths, SHA-256 fingerprint and expiry. The API remains disabled, its
   registry remains unfrozen, and it opens **no API port**. Other configured
   host services still start normally; this is not a certificate-only CLI mode.
3. Stop the instance before editing its configuration. Repeat with a separate
   data root for `login`, using `certificate_dns_names = ["login"]`. Every
   instance must keep its own PFX and private key.

Relative certificate and trust-root paths resolve under `<root>/config`.
Absolute paths are also supported. The public filename is the PFX path **plus
`.pem`**, so `tls/server.pfx` produces `tls/server.pfx.pem`.

| File | Contents | Share with peers? |
| --- | --- | --- |
| `server.pfx` | Local certificate and private key | No; keep with its owning endpoint and protect backups. |
| `server.pfx.pem` | Public certificate only | Yes; peers use it as a trust anchor for this self-signed identity. |

Generation is opt-in: `auto_generate_certificate` defaults to `false`.
With both it and `enabled` false, the API service does no certificate I/O.
With generation enabled, local certificate configuration and files are validated
even when the API listener is disabled. Trust roots and peers are required only
when enabling the listener.

## Exchange public certificates and enable mutual trust

Copy each **public PEM** to the other endpoint using your deployment tooling,
and verify the fingerprint against the originating host's startup log over a
trusted channel. Never copy the PFX to another peer. For example:

| Instance | Local identity | Public certificate copied from the other instance |
| --- | --- | --- |
| `login` | `config/tls/server.pfx` | `config/tls/realm-1.pem` |
| `realm-1` | `config/tls/server.pfx` | `config/tls/login.pem` |

Use OpenSSL to inspect the received public certificate:

```sh
openssl x509 -in /path/to/config/tls/realm-1.pem -noout -subject -issuer -dates -ext subjectAltName
openssl x509 -in /path/to/config/tls/realm-1.pem -noout -fingerprint -sha256
```

OpenSSL prints colon-separated fingerprint bytes. For TOML use exactly the
64 hexadecimal characters, without colons or the `sha256 Fingerprint=` prefix.
The Moongate startup log already uses that form.

On **login**, configure the realm's public certificate and fingerprint:

```toml
[api]
enabled = true
auto_generate_certificate = true
listen_address = "0.0.0.0"
port = 2594
certificate_path = "tls/server.pfx"
certificate_password_environment_variable = ""
certificate_dns_names = ["login"]
certificate_ip_addresses = []
trusted_root_paths = ["tls/realm-1.pem"]

[[api.peers]]
# Replace with the actual SHA-256 fingerprint of realm-1's public certificate.
certificate_sha256 = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"
peer_id = "realm-1"
allowed_operations = ["*"]
```

On **realm-1**, use the same structure with its own DNS name `realm-1`,
`trusted_root_paths = ["tls/login.pem"]`, the **login** certificate fingerprint,
and `peer_id = "login"`. Remove the earlier `peers = []` line when adding
`[[api.peers]]` tables. Restart each instance to load the policy and bind the port.

`allowed_operations = ["*"]` grants this authenticated peer every registered
operation, including operations added later. Use it for a fully trusted login,
realm or administration process. The wildcard grants permissions; it does not
register handlers or make unknown operations callable.

| Value | Permission |
| --- | --- |
| `["*"]` | All operations, including future registrations. |
| `[100, 200]` | Only the listed operation IDs. |
| `[]` or omitted | Authentication is allowed, but every incoming operation is denied. |

The `*` must be quoted inside the array. Mixed lists such as `["*", 100]`,
unknown strings and operation IDs outside 1–65535 are rejected. A trusted
certificate still needs a fingerprint entry; a fingerprint entry still needs
a valid trusted certificate. Private-network placement and wildcard operation
permissions do not bypass these checks.

For an outbound `ApiClient`, configure `ApiTlsOptions` with the local PFX as
`Certificate`, the destination's public PEM as a `TrustedRoots` entry, and its
SHA-256 fingerprint mapped to the expected `ApiPeerIdentity` in
`PeersByCertificateSha256`. For all operations in C#, use
`new ApiPeerIdentity("realm-1", [], allowAllOperations: true)`. Connecting to realm-1 uses target host `realm-1`
and expected peer ID `realm-1`. The host's `[api]` section configures its
**listener**, not an outbound client or realm discovery.

## Passwords and filesystem permissions

`certificate_password_environment_variable = ""` explicitly selects a
passwordless PFX. It is suitable when the runtime account alone can access the
private file and its backups are protected.

On Unix, newly generated PFX files are created with owner read/write permissions
(`0600`, subject to the process umask), and newly created certificate directories
with owner-only access (`0700`, subject to umask). Existing files and directory
permissions are not changed. On Windows, files inherit the directory's ACL;
restrict it to the service account and administrators before provisioning.

To use an encrypted PFX, set a variable **name**, for example:

```toml
certificate_password_environment_variable = "MOONGATE_API_CERTIFICATE_PASSWORD"
```

Inject the password into that environment variable through your credential
provider. It is used for both generation and loading. A configured but unset
variable fails startup; Moongate does not silently generate a passwordless file.
Do not put the password value in TOML, command arguments, source control or logs.
Changing this option does not re-encrypt an existing PFX.

## Docker

Use a persistent, writable volume for generation. With the standard
`MOONGATE_ROOT=/data`, the identity lives under `/data/config/tls`. A minimal
service using a locally built image is:

```yaml
services:
  realm-1:
    image: moongate:local
    ports:
      - "2593:2593"
    volumes:
      - realm-1-data:/data
      - /absolute/path/to/ultima:/uo:ro

volumes:
  realm-1-data:
```

Prepare `/data/config/moongate.toml` as above, including your existing Ultima and
world settings. The image's non-root runtime account must be able to write the
certificate directory. New named data volumes inherit the ownership prepared
by the image; host bind mounts need matching permissions. Use a distinct data
volume for every instance.

Do not use a read-only certificate mount for the initial generation. After
provisioning, a matching PFX/public PEM pair can be reused on a read-only mount.
If the public PEM is missing or differs, generation mode needs write access to
recreate it from the existing PFX. You can instead disable generation when all
certificates are externally managed.

Peers on the same Compose network can reach `realm-1:2594` after API enablement
without publishing that port to the host. A client using that target must trust
a certificate with DNS SAN `realm-1`. Keep the data volume across container
replacements: losing it causes a new identity to be generated, which existing
peer allowlists correctly reject. See [Docker deployment](docker.md#internal-api-port)
for builds, externally provisioned certificates and private port mappings.

## Existing certificates, renewal and revocation

The generated identity uses RSA 2048, SHA-256 signatures, both TLS `serverAuth`
and `clientAuth` usages, and one-year validity. Its validity starts five minutes
before creation to tolerate small clock differences. It is not a CA and must
not be used to issue certificates for other nodes.

Existing valid PFX files are reused unchanged, including when DNS/IP settings
change. Existing invalid, expired, future-dated, password-mismatched or
private-key-less files fail startup and are **never replaced automatically**.
Concurrent attempts to create the same missing PFX converge on the first
complete identity published. A missing public PEM is recreated from that
identity; export failure preserves the PFX so a retry does not rotate the key.

Renewal is an explicit deployment operation. For a coordinated restart:

1. Stop affected API endpoints and retain a protected backup of the old identity
   if rollback is required. Choose a new unused path, such as `tls/server-next.pfx`.
2. Set `enabled = false`, `auto_generate_certificate = true` and the new
   `certificate_path`, with the desired DNS/IP names. Start to provision the new
   identity, then stop. No API listener opens during this step.
3. Distribute the new public PEM. Update each affected peer's trust-root files
   and SHA-256 allowlist, and update outbound client options too.
4. Set `enabled = true`, restart the affected endpoints and reconnect clients.
   After successful verification, remove obsolete trust roots and fingerprints.

A renewed certificate has a new fingerprint even if its names are unchanged.
For CA-issued certificates, use your issuer's renewal process and keep
`auto_generate_certificate = false`; the same allowlist update still applies.
See [Generate a private CA](../src/Moongate.Api/README.md#generate-a-private-ca)
for the repository's development script.

To revoke a peer, remove its fingerprint, restart the endpoint and reconnect
remaining clients. Existing connections retain their original identity and
permissions until closed. The API does not query CRL/OCSP services.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Disabled warning and no certificate files | Set `auto_generate_certificate = true`, a nonblank PFX path, and the password-variable policy; the API service must reach startup. |
| Password environment variable is not set | Supply the named variable, or explicitly select `""` for a passwordless PFX. |
| Certificate is not valid at the current time | Check host clocks and certificate dates; renew deliberately instead of deleting the identity blindly. |
| Public export fails | Check directory ownership, write permissions and whether the `.pfx.pem` path is a directory; the existing PFX is retained. |
| TLS connection rejected | Check both trust roots and leaf fingerprints, SAN target name, expected peer ID, expiry and TLS usages. |
| `Forbidden` response | Authentication succeeded; the caller's allowlist is missing the requested operation ID. |
| New identity after replacing a container | The original PFX was not preserved in its persistent volume. Restore it, or deliberately distribute the new identity. |

An HTTP request or `curl` cannot probe this protocol. Use an authenticated
`ApiClient` and a registered typed handler to verify a complete round trip.
