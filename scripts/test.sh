#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'EOF'
Usage: scripts/test.sh [fast|all] [dotnet test options]

  fast  Run tests without external services (default). Excludes Integration,
        Performance, and Stress namespaces and Category traits.
  all   Run the full ordinary suite, including PostgreSQL and Redis tests.

Both modes build by default and run test project hosts serially. Pass -c Release
for a release build, or --no-build only after building the same configuration.
Additional dotnet test options are forwarded; --filter narrows either mode.
Use -- before inline runsettings, as with dotnet test.

The all mode requires MOONGATE_TEST_POSTGRES_CONNECTION_STRING and
MOONGATE_TEST_REDIS_CONNECTION_STRING. Existing opt-in persistence stress and
Python administration protocol tests keep their own environment requirements.

Examples:
  scripts/test.sh
  scripts/test.sh fast --filter 'FullyQualifiedName~Serial'
  scripts/test.sh all -c Release
  scripts/test.sh all -c Release --no-build
EOF
}

mode=fast
case "${1:-}" in
    fast|all) mode=$1; shift ;;
    ''|-*) ;;
    *) printf 'Unknown test mode: %s\n' "$1" >&2; usage >&2; exit 2 ;;
esac

filter=
if [[ "$mode" == fast ]]; then
    filter='FullyQualifiedName!~.Integration.&FullyQualifiedName!~.Performance.&FullyQualifiedName!~.Stress.&Category!=Integration&Category!=Performance&Category!=Stress'
fi

args=()
while (($#)); do
    case "$1" in
        -h|--help) usage; exit 0 ;;
        --filter)
            if (($# < 2)) || [[ -z "$2" ]]; then
                printf '%s\n' '--filter requires a non-empty expression.' >&2
                exit 2
            fi
            value=$2
            shift 2
            ;;
        --filter=*) value=${1#*=}; shift ;;
        --) args+=("$@"); break ;;
        *) args+=("$1"); shift; continue ;;
    esac
    if [[ -z "$value" ]]; then
        printf '%s\n' '--filter requires a non-empty expression.' >&2
        exit 2
    fi
    if [[ -n "$filter" ]]; then
        filter="($filter)&($value)"
    else
        filter=$value
    fi
done

selection=()
if [[ -n "$filter" ]]; then
    selection=(--filter "$filter")
fi

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repo_root"
exec dotnet test Moongate.slnx -m:1 "${selection[@]}" "${args[@]}"
