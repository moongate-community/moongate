#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 || ! "$1" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo "Usage: $0 <major.minor.patch>" >&2
  exit 64
fi

version="$1"
root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
props_file="$root_dir/Directory.Build.props"
version_count=$(grep -Ec '<Version>[^<]+</Version>' "$props_file" || true)

if [[ "$version_count" -ne 1 ]]; then
  echo "Expected exactly one <Version> element in $props_file, found $version_count" >&2
  exit 1
fi

sed -i -E "s#<Version>[^<]+</Version>#<Version>${version}</Version>#" "$props_file"

if ! grep -Fq "<Version>${version}</Version>" "$props_file"; then
  echo "Failed to update $props_file to version $version" >&2
  exit 1
fi

echo "Updated Directory.Build.props to version $version"
