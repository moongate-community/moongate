# Run on Kubernetes

The Helm chart in [`deploy/helm/moongate`](../deploy/helm/moongate) runs one login and any number of realms, the same topology as the [Docker login and realms example](docker-login-realms.md). Each release publishes it as an OCI artifact next to the image:

```sh
helm install moongate oci://ghcr.io/moongate-community/charts/moongate --version <release> -n moongate --create-namespace -f values.yaml
```

To run the chart from a checkout, use `deploy/helm/moongate` in place of the `oci://` address.

## Before you install

- Kubernetes 1.26 or later, and a way to reach the Services from your players: a `LoadBalancer` (the chart's default), or `NodePort`. The image is built for `linux/amd64` only.
- PostgreSQL 16 and Redis 7.4 that the cluster can reach, or the bundled trial copies (see [Trial PostgreSQL and Redis](#trial-postgresql-and-redis)).
- A PersistentVolumeClaim with your Ultima Online client files. They are not distributed with Moongate. The chart mounts the claim read-only on `/uo` in every Moongate pod, so pods on different nodes need a `ReadOnlyMany` or `ReadWriteMany` volume (or a `nodeSelector` that keeps them together).

## Minimal values

```yaml
uo:
  existingClaim: uo-files
secrets:
  existingSecret: moongate-secrets
realms:
  - id: realm-1
    name: "Moongate Realm 1"
    serverIndex: 1
    advertisedAddress: 192.168.255.30
```

`helm install` refuses values without `uo.existingClaim`, a realm without `advertisedAddress` (an IPv4 literal), two realms with the same `id` or `serverIndex`, an `id` that is not a lowercase DNS label, `ping` enabled on more than one realm, a realm `id` the chart uses for itself (`accounts`, `login`, `postgresql`, `redis`, `generated`), names longer than 52 characters, and an `advertisedPort` outside 30000-32767 when the realm's `service.type` is `NodePort` (the advertised port is then the node port).

## What it creates

| Resource | Notes |
| --- | --- |
| `<release>-moongate-login` Deployment and Service | One replica, TCP 2593. `mode = "login"`. |
| `<release>-moongate-<id>` StatefulSet, ConfigMap and Service per realm | One replica, TCP 2595, its own `/data` volume and database. `mode = "game"`. A realm is not scaled: one process owns one root and one Realm database. |
| Secret | Only with the trial dependencies; otherwise the Secret is yours. |
| PostgreSQL, Redis | Only with `postgresql.enabled` and `redis.enabled`. |
| NetworkPolicy | Only with `networkPolicy.enabled`: the trial PostgreSQL and Redis accept this release's pods only. |

Every pod runs as the image's non-root user with all capabilities dropped and no service account token. Two init containers run before the server: `mgctl init /data` adds the data files, templates and scripts a release ships (the volume keeps the ones it already has), then `mgctl migrate apply` applies the SQL of the pod's database (`auth` for the login, `world` for a realm) with the schema credentials. Set `schema.enabled: false` to apply SQL yourself with [`mgctl migrate`](persistence-migrations.md). Each pod owns its database, so two applies never run at once.

## Secrets

The chart never writes a password into a ConfigMap or into the TOML files: the files hold `$MOONGATE_*` references and the pods read them from one Secret, `secrets.existingSecret`. Create it with these keys:

| Key | Content |
| --- | --- |
| `accounts-runtime-url` | `postgres://` URI of the Accounts database, runtime role |
| `accounts-schema-url` | The same database, schema role (read by the init container only) |
| `<id>-runtime-url`, `<id>-schema-url` | The same for each realm, for example `realm-1-runtime-url` |
| `redis-connection-string` | StackExchange.Redis string, for example `redis.example:6379,password=<secret>` |
| `handoff-secret` | At least 64 hexadecimal characters, shared by the login and every realm |

```sh
kubectl create secret generic moongate-secrets -n moongate \
  --from-literal=accounts-runtime-url="$ACCOUNTS_RUNTIME_URL" \
  --from-literal=accounts-schema-url="$ACCOUNTS_SCHEMA_URL" \
  --from-literal=realm-1-runtime-url="$REALM_1_RUNTIME_URL" \
  --from-literal=realm-1-schema-url="$REALM_1_SCHEMA_URL" \
  --from-literal=redis-connection-string="$REDIS_CONNECTION_STRING" \
  --from-literal=handoff-secret="$HANDOFF_SECRET"
```

Take the values from your secret manager; do not write them in a values file. Percent-encode the user name and the password inside the `postgres://` URIs, and keep the Redis password free of commas: a hexadecimal password is the safe choice. Roles and databases are described in [Persistence migrations](persistence-migrations.md); the Compose example's [`init.sh`](../examples/docker/login-realms/postgres/init.sh) creates them for PostgreSQL.

## Realms and the address clients use

The login sends the client to a realm's `advertisedAddress` and `advertisedPort` (packets `0xA8` and `0x8C`). They must be the external address and port of that realm's Service, and the chart cannot know a `LoadBalancer` address before it exists. Either set `loadBalancerIP` in the Service values (`login.service` or `realms[].service`; some load balancers ignore it and want an annotation, which `annotations` carries) to an address you know, or install once, read the addresses and upgrade:

```sh
kubectl get svc -n moongate -l app.kubernetes.io/instance=moongate
helm upgrade moongate oci://ghcr.io/moongate-community/charts/moongate --version <release> -n moongate -f values.yaml
```

To add a realm, append an entry to `realms` and run `helm upgrade`; with your own PostgreSQL, create its roles and database and add its two keys to your Secret first.

The login's Service port is `login.service.port` (2593). A realm's Service listens on its `advertisedPort` and forwards to 2595.

| Value | Meaning |
| --- | --- |
| `realms[].persistence.size`, `storageClass` | The realm's `/data` volume, 5Gi by default; set at install time only, because Kubernetes does not change the volume claims of a StatefulSet |
| `realms[].ping.enabled` | Opens the UDP ping port 12000 on that realm's Service. One realm only: two cannot share an address |
| `realms[].service.type`, `annotations`, `loadBalancerIP` | `LoadBalancer`, `NodePort` or `ClusterIP`; `externalTrafficPolicy: Local` keeps the players' addresses in the logs |
| `realms[].extraToml`, `login.extraToml` | Text appended at the end of the server's `moongate.toml`, after `[admin_api]`. Start it with a table header such as `[ultima.crime]`; it cannot repeat a table the chart already writes (`shard`, `network`, `network.encryption`, `ultima`, `persistence`, `redis`, `realm_directory`, `admin_api`). See the [settings](server-configuration.md) |
| `network.encryptionMode` | `Disabled`, `Optional` or `Required` for both UO listeners |
| `image.tag` | Defaults to the chart's `appVersion`, the release it was published with |
| `login.shardName`, `login.service.annotations`, `login.service.nodePort` | The login's shard name, Service annotations and, with `NodePort`, its node port |
| `resources`, `nodeSelector` under `login` and under each realm | Passed to the pods as they are |
| `imagePullSecrets`, `podSecurity` | Registry credentials for the image; the user and group of the pods (1654, the image's user) |
| `postgresql.image`, `postgresql.storage`, `redis.image` | Images and volume of the trial dependencies, pinned by digest |

## Trial PostgreSQL and Redis

`postgresql.enabled` and `redis.enabled` (both, with no `secrets.existingSecret`) install a single-pod PostgreSQL 16 and a Redis 7.4 with `noeviction`, no persistence, and generated passwords. They exist to try the chart, not to run a shard: no backups, no replica, and the roles and databases are created on the first start of an empty volume. A realm added later leaves its pod in `Init:CrashLoopBackOff` on the `migrate` container until you run `kubectl exec <release>-moongate-postgresql-0 -- /docker-entrypoint-initdb.d/10-moongate.sh`, which is safe to repeat.

The passwords are in the Secret `<release>-moongate-generated`, which stays after `helm uninstall` so a reinstall can still open the databases. Delete it with `kubectl delete secret` together with the PostgreSQL volume when you want a clean start. `helm template` and `--dry-run` cannot read existing Secrets, so they show passwords that differ from the installed ones. For the same reason Argo CD and Flux, which render the chart that way, would generate new passwords at every sync: give them your own Secret.

## Update and stop

An update is a new `--version` (or `image.tag`) and `helm upgrade`. The pod of each realm restarts, applies the pending SQL in its init container, and starts; one pod per realm means a short outage of that realm during the update. Read the target release's [changelog](../CHANGELOG.md) first and follow your database backup policy.

The pods get 120 seconds (`terminationGracePeriodSeconds`) to stop, because the final world save takes 8 to 15 seconds for the shipped world. World saves are not PostgreSQL backups. `helm uninstall` keeps the PersistentVolumeClaims of the realms' StatefulSets.

## Troubleshooting

- **The realm pod restarts with `tiledata.mul not found`:** the client files claim is empty or not the one in `uo.existingClaim`.
- **A pod stays in `Init:CrashLoopBackOff` on `migrate`:** read `kubectl logs <pod> -c migrate`; a role or password error means the database and the schema credentials in the Secret do not match.
- **Players reach the login but cannot enter a realm:** `advertisedAddress` and `advertisedPort` are not the realm Service's external address and port.
- **The log shows a client that connects and leaves every few seconds:** that is the readiness probe, a plain TCP connect.

## Not covered

Standalone mode, the sample plugin, the administration API (it needs a TLS certificate Secret), an Ingress, autoscaling, and a smoke test on a throwaway cluster in CI. The chart is checked by `helm lint`, the render assertions in `deploy/helm/moongate/tests` and `kubeconform` on every change.

## What was tried

The chart passes lint, its render assertions and the Kubernetes schemas. It was installed on a five-node k3s cluster in a throwaway namespace with the trial PostgreSQL and Redis and an empty client-files claim: the roles and databases were created, the init containers applied 4 migrations to Auth and 26 to World, the login started and listened, and the realm stopped at `tiledata.mul not found in the Ultima path: /uo`, as it must without client files. `helm upgrade` kept the generated passwords and restarted only the pod whose configuration changed.

Not tried yet: a real client, a `ReadWriteMany` volume, `LoadBalancer` addresses, the NetworkPolicy, and installing from the published OCI address (the first publication happens with the next release).
