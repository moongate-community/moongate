#!/usr/bin/env bash
set -euo pipefail
repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
version=${1:?Usage: package-admin-protos.sh version}
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-+][0-9A-Za-z.-]+)?$ ]]; then
    echo "Invalid release version." >&2
    exit 1
fi
mkdir -p "$repo_root/artifacts"
python3 - "$repo_root" "$version" <<'PY'
import sys, zipfile
from pathlib import Path
root, version = Path(sys.argv[1]), sys.argv[2]
with zipfile.ZipFile(root / 'artifacts' / f'moongate-admin-protos-{version}.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
    for file in sorted((root / 'src/Moongate.Admin.Contracts/proto').rglob('*.proto')):
        archive.write(file, 'proto/' + str(file.relative_to(root / 'src/Moongate.Admin.Contracts/proto')))
    archive.write(root / 'LICENSE', 'LICENSE')
    archive.writestr('README.md', '# Moongate administration contracts\n\nUse proto/ as the protoc include root. Standard google/protobuf imports come from your compiler distribution.\n\nPython: python -m grpc_tools.protoc -I proto --python_out=. --grpc_python_out=. proto/moongate/admin/v1/*.proto\n\nAPI package: moongate.admin.v1. Connect using server TLS and authorization: Bearer <token>. See the Moongate administration documentation for login, roles and permissions.\n')
PY
