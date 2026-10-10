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

# TASK-MARKER: assertions of the next tasks are appended above this line.
exit $status
