#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 || ! "$1" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo "Usage: $0 <major.minor.patch>" >&2
  exit 64
fi

version="$1"
root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
props_file="$root_dir/Directory.Build.props"
declare -A versions=(
  [Version]="$version"
  [AssemblyVersion]="${version}.0"
  [FileVersion]="${version}.0"
  [InformationalVersion]="$version"
)

for property in Version AssemblyVersion FileVersion InformationalVersion; do
  property_count=$(grep -Ec "<${property}>[^<]+</${property}>" "$props_file" || true)

  if [[ "$property_count" -ne 1 ]]; then
    echo "Expected exactly one <${property}> element in $props_file, found $property_count" >&2
    exit 1
  fi

  property_version="${versions[$property]}"
  sed -i -E "s#<${property}>[^<]+</${property}>#<${property}>${property_version}</${property}>#" "$props_file"

  if ! grep -Fq "<${property}>${property_version}</${property}>" "$props_file"; then
    echo "Failed to update ${property} in $props_file to $property_version" >&2
    exit 1
  fi
done

echo "Updated Directory.Build.props to version $version"
