#!/usr/bin/env bash
# Render assertions for the Moongate chart. Needs helm.
set -euo pipefail
cd "$(dirname "$0")/.."
status=0
pass() { echo "ok   - $1"; }
fail() { echo "FAIL - $1"; status=1; }
render() { helm template t . --namespace ns "$@"; }
# expect_contains <description> <pattern> <helm args...>
expect_contains() { local d=$1 p=$2; shift 2; if render "$@" 2>&1 | grep -Eq -- "$p"; then pass "$d"; else fail "$d"; fi; }
expect_absent() { local d=$1 p=$2; shift 2; if render "$@" 2>&1 | grep -Eq -- "$p"; then fail "$d"; else pass "$d"; fi; }
# expect_error <description> <message pattern> <helm args...>
expect_error() { local d=$1 p=$2; shift 2; local out; if out=$(render "$@" 2>&1); then fail "$d (rendered)"; elif grep -Eq -- "$p" <<<"$out"; then pass "$d"; else fail "$d: $out"; fi; }

expect_error "defaults need the client files claim" "uo.existingClaim"
expect_error "advertised address is required" "advertisedAddress" --set uo.existingClaim=uo --set 'realms[0].id=realm-1' --set 'realms[0].name=R' --set 'realms[0].serverIndex=1'
expect_error "advertised address must be IPv4" "advertisedAddress" -f ci/external.yaml --set 'realms[0].advertisedAddress=host.example'
expect_error "duplicate realm id" "duplicate realm id" -f ci/two-realms.yaml --set 'realms[1].id=realm-1'
expect_error "duplicate server index" "duplicate serverIndex" -f ci/two-realms.yaml --set 'realms[1].serverIndex=1'
expect_error "realm id must be a DNS label" "realms.*id" -f ci/external.yaml --set 'realms[0].id=Realm_1'
expect_error "ping on two realms" "ping" -f ci/two-realms.yaml --set 'realms[0].ping.enabled=true' --set 'realms[1].ping.enabled=true'

# Task 2: secret
expect_absent "external secret: the chart renders no Secret" "kind: Secret" -f ci/external.yaml
expect_error "no secret and no bundled dependencies" "existingSecret" -f ci/external.yaml --set secrets.existingSecret=
expect_error "existing secret with bundled dependencies" "existingSecret" -f ci/bundled.yaml --set secrets.existingSecret=x
expect_contains "bundled: runtime url of accounts" "accounts-runtime-url:" -f ci/bundled.yaml
expect_contains "bundled: runtime url of a realm" "realm-1-runtime-url:" -f ci/bundled.yaml
expect_contains "bundled: schema url of a realm" "realm-1-schema-url:" -f ci/bundled.yaml
expect_contains "bundled: redis string" "redis-connection-string:" -f ci/bundled.yaml
expect_contains "bundled: handoff secret" "handoff-secret:" -f ci/bundled.yaml
expect_contains "bundled: generated secret kept on uninstall" "helm.sh/resource-policy: keep" -f ci/bundled.yaml
expect_contains "bundled: database host" "@t-moongate-postgresql:5432/moongate_realm_1" -f ci/bundled.yaml

# Task 3: login
expect_contains "login: deployment" "name: t-moongate-login" -f ci/external.yaml
expect_contains "login: kind Deployment" "kind: Deployment" -f ci/external.yaml
expect_contains "login: container port" "containerPort: 2593" -f ci/external.yaml
expect_contains "login: client files claim" "claimName: uo-files" -f ci/external.yaml
expect_contains "login: client files read-only" "readOnly: true" -f ci/external.yaml
expect_contains "login: mode" 'mode = "login"' -f ci/external.yaml
expect_contains "login: no ping server" "enable_ping_server = false" -f ci/external.yaml
expect_contains "login: non-root" "runAsNonRoot: true" -f ci/external.yaml
expect_contains "login: config checksum" "checksum/config:" -f ci/external.yaml
expect_contains "login: accounts env" "name: MOONGATE_ACCOUNTS_DATABASE" -f ci/external.yaml
expect_contains "login: accounts key" "key: accounts-runtime-url" -f ci/external.yaml
expect_contains "login: user secret name" "name: moongate-secrets" -f ci/external.yaml
expect_contains "login: schema init" "--target auth" -f ci/external.yaml
expect_absent "login: schema init off" "accounts-schema-url" -f ci/external.yaml --set schema.enabled=false
expect_contains "login: encryption mode" 'mode = "Optional"' -f ci/external.yaml --set network.encryptionMode=Optional
expect_contains "login: service" "port: 2593" -f ci/external.yaml
expect_contains "login: load balancer" "type: LoadBalancer" -f ci/external.yaml
expect_contains "login: extra toml" "custom = 1" -f ci/external.yaml --set-string login.extraToml="custom = 1"
expect_contains "login: bundled secret name" "name: t-moongate$" -f ci/bundled.yaml

# Task 4: realms
expect_contains "realms: statefulset 1" "name: t-moongate-realm-1$" -f ci/two-realms.yaml
expect_contains "realms: statefulset 2" "name: t-moongate-realm-2$" -f ci/two-realms.yaml
expect_contains "realms: kind" "kind: StatefulSet" -f ci/two-realms.yaml
expect_contains "realms: mode" 'mode = "game"' -f ci/two-realms.yaml
expect_contains "realms: server index" "server_index = 2" -f ci/two-realms.yaml
expect_contains "realms: advertised address" 'advertised_address = "192.168.255.31"' -f ci/two-realms.yaml
expect_contains "realms: volume claim template" "volumeClaimTemplates" -f ci/two-realms.yaml
expect_contains "realms: grace period" "terminationGracePeriodSeconds: 120" -f ci/two-realms.yaml
expect_contains "realms: game port" "containerPort: 2595" -f ci/two-realms.yaml
expect_contains "realms: runtime key" "key: realm-2-runtime-url" -f ci/two-realms.yaml
expect_contains "realms: schema key" "key: realm-2-schema-url" -f ci/two-realms.yaml
expect_contains "realms: world target" "--target world" -f ci/two-realms.yaml
expect_absent "realms: schema init off" "--target world" -f ci/two-realms.yaml --set schema.enabled=false
expect_absent "realms: no ping by default" "12000" -f ci/two-realms.yaml
expect_contains "realms: ping udp" "protocol: UDP" -f ci/two-realms.yaml --set 'realms[0].ping.enabled=true'
expect_contains "realms: ping server on" "enable_ping_server = true" -f ci/two-realms.yaml --set 'realms[0].ping.enabled=true'
expect_contains "realms: storage class" "storageClassName: longhorn" -f ci/two-realms.yaml --set 'realms[0].persistence.storageClass=longhorn'
expect_contains "realms: service port" "port: 2595" -f ci/two-realms.yaml
expect_contains "realms: custom advertised port" "advertised_port = 2600" -f ci/two-realms.yaml --set 'realms[0].advertisedPort=2600'
expect_contains "realms: config checksum" "checksum/config:" -f ci/two-realms.yaml

# Task 5: bundled dependencies
expect_contains "bundled: postgres statefulset" "name: t-moongate-postgresql$" -f ci/bundled.yaml
expect_contains "bundled: postgres admin password" "name: POSTGRES_PASSWORD" -f ci/bundled.yaml
expect_contains "bundled: init creates the realm roles" "provision_role moongate_realm_1_schema" -f ci/bundled.yaml
expect_contains "bundled: init creates the accounts roles" "provision_role moongate_accounts_runtime" -f ci/bundled.yaml
expect_contains "bundled: world schema" "provision_schema moongate_realm_1 world" -f ci/bundled.yaml
expect_contains "bundled: auth schema" "provision_schema moongate_accounts auth" -f ci/bundled.yaml
expect_contains "bundled: postgres volume" "storage: 5Gi" -f ci/bundled.yaml
expect_contains "bundled: redis deployment" "name: t-moongate-redis$" -f ci/bundled.yaml
expect_contains "bundled: redis password" "--requirepass" -f ci/bundled.yaml
expect_contains "bundled: redis eviction" "noeviction" -f ci/bundled.yaml
expect_absent "external: no postgres" "t-moongate-postgresql" -f ci/external.yaml
expect_absent "external: no redis" "t-moongate-redis" -f ci/external.yaml
expect_contains "bundled: second realm roles" "provision_role moongate_realm_2_runtime" -f ci/bundled.yaml --set 'realms[1].id=realm-2' --set 'realms[1].name=R2' --set 'realms[1].serverIndex=2' --set 'realms[1].advertisedAddress=192.168.255.31'

# Task 6: network policy and notes
expect_absent "network policy off by default" "kind: NetworkPolicy" -f ci/bundled.yaml
expect_contains "network policy on" "kind: NetworkPolicy" -f ci/bundled.yaml --set networkPolicy.enabled=true
if helm install t . -f ci/bundled.yaml --dry-run=client -n ns 2>&1 | grep -q "advertisedAddress"; then pass "notes mention advertisedAddress"; else fail "notes mention advertisedAddress"; fi
if helm install t . -f ci/bundled.yaml --dry-run=client -n ns 2>&1 | grep -qi "trial"; then pass "notes warn about trial dependencies"; else fail "notes warn about trial dependencies"; fi

# TASK-MARKER: assertions of the next tasks are appended above this line.
exit $status
