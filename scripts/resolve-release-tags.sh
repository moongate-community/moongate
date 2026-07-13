#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 || ! "$1" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo "Usage: $0 <major.minor.patch>" >&2
  exit 64
fi

release_version="$1"
stable_tag_pattern='^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'
mapfile -t stable_versions < <(
  while IFS= read -r tag; do
    if [[ "$tag" =~ $stable_tag_pattern ]]; then
      echo "${tag#v}"
    fi
  done < <(git tag --list)
)
mapfile -t stable_versions < <(printf '%s\n' "${stable_versions[@]}" | sort -V)

if [[ "${#stable_versions[@]}" -eq 0 ]]; then
  echo "No stable release tags found" >&2
  exit 1
fi

current_major="${release_version%%.*}"
current_minor="${release_version%.*}"
latest_version="${stable_versions[$((${#stable_versions[@]} - 1))]}"
latest_major=''
latest_minor=''
current_found=false

for version in "${stable_versions[@]}"; do
  if [[ "$version" == "$release_version" ]]; then
    current_found=true
  fi
  if [[ "${version%%.*}" == "$current_major" ]]; then
    latest_major="$version"
  fi
  if [[ "${version%.*}" == "$current_minor" ]]; then
    latest_minor="$version"
  fi
done

if [[ "$current_found" != true ]]; then
  echo "Release tag v$release_version was not found" >&2
  exit 1
fi

update_latest=false
update_major=false
update_minor=false
if [[ "$release_version" == "$latest_version" ]]; then
  update_latest=true
fi
if [[ "$release_version" == "$latest_major" ]]; then
  update_major=true
fi
if [[ "$release_version" == "$latest_minor" ]]; then
  update_minor=true
fi

echo "major_tag=$current_major"
echo "minor_tag=$current_minor"
echo "update_latest=$update_latest"
echo "update_major=$update_major"
echo "update_minor=$update_minor"
