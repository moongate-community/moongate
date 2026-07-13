#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
temp_dir=$(mktemp -d)
trap 'rm -rf "$temp_dir"' EXIT

git -C "$temp_dir" init --quiet
git -C "$temp_dir" config user.name test
git -C "$temp_dir" config user.email test@example.com
git -C "$temp_dir" config commit.gpgsign false
git -C "$temp_dir" config tag.gpgsign false
git -C "$temp_dir" commit --quiet --allow-empty -m baseline

for tag in v0.1.0 v0.2.0 v0.2.1 v1.0.0; do
  git -C "$temp_dir" tag "$tag"
done

git -C "$temp_dir" update-ref refs/remotes/origin/main HEAD
git -C "$temp_dir" commit --quiet --allow-empty -m off-main
git -C "$temp_dir" tag v9.0.0
git -C "$temp_dir" reset --quiet --hard HEAD^

assert_output() {
  local version="$1"
  local expected_latest="$2"
  local expected_major="$3"
  local expected_minor="$4"
  local output

  output=$(cd "$temp_dir" && "$root_dir/scripts/resolve-release-tags.sh" "$version")
  grep -Fqx "update_latest=$expected_latest" <<< "$output"
  grep -Fqx "update_major=$expected_major" <<< "$output"
  grep -Fqx "update_minor=$expected_minor" <<< "$output"
}

assert_output 0.1.0 false false true
assert_output 0.2.0 false false false
assert_output 0.2.1 false true true
assert_output 1.0.0 true true true

if (cd "$temp_dir" && "$root_dir/scripts/resolve-release-tags.sh" 0.3.0); then
  echo "Expected a missing release tag to be rejected" >&2
  exit 1
fi

echo "release tag resolution tests passed"
