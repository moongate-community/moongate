#!/usr/bin/env bash

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd "${script_dir}/.." && pwd)"
plugin_id="__PLUGIN_ID__"
project_name="PluginTemplate"
configuration="${1:-Release}"
publish_dir="${project_root}/artifacts/publish"
runtime_dir="${project_root}/artifacts/${plugin_id}"
zip_path="${project_root}/artifacts/${plugin_id}.zip"

rm -rf "${publish_dir}" "${runtime_dir}" "${zip_path}"

dotnet publish "${project_root}/${project_name}.csproj" -c "${configuration}" -o "${publish_dir}"

mkdir -p "${runtime_dir}/bin"
cp "${project_root}/manifest.json" "${runtime_dir}/manifest.json"
cp -R "${publish_dir}/." "${runtime_dir}/bin/"

for directory in data scripts assets; do
    if [ -d "${project_root}/${directory}" ]; then
        mkdir -p "${runtime_dir}/${directory}"
        cp -R "${project_root}/${directory}/." "${runtime_dir}/${directory}/"
    fi
done

(
    cd "${project_root}/artifacts"
    zip -rq "${plugin_id}.zip" "${plugin_id}"
)

echo "Created plugin runtime directory at ${runtime_dir}"
echo "Created plugin archive at ${zip_path}"
