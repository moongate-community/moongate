#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
temp_dir=$(mktemp -d)
trap 'rm -rf "$temp_dir"' EXIT

mkdir -p "$temp_dir/scripts"
cp "$root_dir/scripts/semantic-release-prepare.sh" "$temp_dir/scripts/"
cp "$root_dir/Directory.Build.props" "$temp_dir/"

"$temp_dir/scripts/semantic-release-prepare.sh" 9.8.7

grep -Fqx '        <Version>9.8.7</Version>' "$temp_dir/Directory.Build.props"
grep -Fqx '        <AssemblyVersion>9.8.7.0</AssemblyVersion>' "$temp_dir/Directory.Build.props"
grep -Fqx '        <FileVersion>9.8.7.0</FileVersion>' "$temp_dir/Directory.Build.props"
grep -Fqx '        <InformationalVersion>9.8.7</InformationalVersion>' "$temp_dir/Directory.Build.props"

if "$temp_dir/scripts/semantic-release-prepare.sh" 09.8.7; then
  echo "Expected an invalid semantic version to be rejected" >&2
  exit 1
fi

echo "semantic-release prepare script tests passed"
