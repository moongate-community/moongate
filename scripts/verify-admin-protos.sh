#!/usr/bin/env bash
set -euo pipefail
repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
admin_temp=$(mktemp -d)
trap 'rm -rf "$admin_temp"' EXIT
python3 -m venv "$admin_temp/venv"
"$admin_temp/venv/bin/pip" -q install -r "$repo_root/samples/admin-python/requirements.txt"
mkdir -p "$admin_temp/generated"
"$admin_temp/venv/bin/python" -m grpc_tools.protoc \
  -I "$repo_root/src/Moongate.Admin.Contracts/proto" \
  --python_out="$admin_temp/generated" --grpc_python_out="$admin_temp/generated" \
  "$repo_root"/src/Moongate.Admin.Contracts/proto/moongate/admin/v1/*.proto
export MOONGATE_ADMIN_PYTHON="$admin_temp/venv/bin/python"
export MOONGATE_ADMIN_PYTHON_CLIENT="$repo_root/samples/admin-python/client.py"
export MOONGATE_ADMIN_PROTO_PATH="$admin_temp/generated"
dotnet test "$repo_root/tests/Moongate.Tests" -c Release \
  --filter FullyQualifiedName~AdminProtocolTests --verbosity normal
