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

# TASK-MARKER: assertions of the next tasks are appended above this line.
exit $status
