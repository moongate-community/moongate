#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'USAGE'
Usage: scripts/run_server.sh [--root-directory <path>] [--skip-build] [--build-only] [server options]

Publishes a Release build of the server, mgboot and the migration runner into a
clean dist/moongate, prepares the root with mgboot and starts the server on it.

  --root-directory <path>  Server root. Defaults to MOONGATE_ROOT.
  --skip-build             Start the build already in dist/moongate.
  --build-only             Publish into dist/moongate and stop; needs no root.
  -h, --help               Show this help.

mgboot adds the shipped data, template and script files the root lacks and
keeps the ones already there. Every other option goes to Moongate.Server as it is.

Examples:
  scripts/run_server.sh --root-directory "$HOME/moongate"
  scripts/run_server.sh --root-directory "$HOME/moongate" --skip-build
  scripts/run_server.sh --root-directory "$HOME/moongate" --log-level Debug --log-packets
  scripts/run_server.sh --build-only
USAGE
}

root_directory=${MOONGATE_ROOT:-}
skip_build=false
build_only=false
server_options=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --root-directory)
            [[ $# -ge 2 ]] || { printf '%s needs a path.\n' "$1" >&2; exit 2; }
            root_directory=$2
            shift 2
            ;;
        --root-directory=*) root_directory=${1#*=}; shift ;;
        --skip-build) skip_build=true; shift ;;
        --build-only) build_only=true; shift ;;
        -h|--help) usage; exit 0 ;;
        --) shift; server_options+=("$@"); break ;;
        *) server_options+=("$1"); shift ;;
    esac
done

if [[ "$skip_build" == true && "$build_only" == true ]]; then
    printf -- '--skip-build and --build-only cannot be used together.\n' >&2
    exit 2
fi

if [[ "$build_only" == false && -z "$root_directory" ]]; then
    printf 'Pass --root-directory <path> or set MOONGATE_ROOT.\n' >&2
    exit 2
fi

repository_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
dist_directory="$repository_root/dist/moongate"

if [[ "$skip_build" == false ]]; then
    printf 'Publishing the Release build into %s\n' "$dist_directory"
    # A clean output: an incremental publish over an old one can leave files out, such as the bundled migrations.
    rm -rf -- "$dist_directory"
    dotnet publish "$repository_root/src/Moongate.Server/Moongate.Server.csproj" \
        -c Release -o "$dist_directory" --nologo -v quiet
    dotnet publish "$repository_root/src/Moongate.MigrationRunner/Moongate.MigrationRunner.csproj" \
        -c Release -o "$dist_directory/migration-runner" --nologo -v quiet
    dotnet publish "$repository_root/src/Moongate.Boot/Moongate.Boot.csproj" \
        -c Release -o "$dist_directory" --nologo -v quiet
fi

for binary in Moongate.Server mgboot; do
    if [[ ! -x "$dist_directory/$binary" ]]; then
        printf '%s not found in %s; run without --skip-build.\n' "$binary" "$dist_directory" >&2
        exit 1
    fi
done

if [[ "$build_only" == true ]]; then
    printf 'Release ready in %s\n' "$dist_directory"
    exit 0
fi

# A configuration may name its paths through ${MOONGATE_ROOT}, such as migrations_directory.
export MOONGATE_ROOT=${MOONGATE_ROOT:-$root_directory}

"$dist_directory/mgboot" "$root_directory"

exec "$dist_directory/Moongate.Server" --root-directory "$root_directory" "${server_options[@]}"
