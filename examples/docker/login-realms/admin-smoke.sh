#!/usr/bin/env bash
# Disposable private-network TLS smoke test. Never uses an existing Compose project.
set -euo pipefail
example=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
repository=$(cd "$example/../../.." && pwd)
project="moongate-admin-smoke-$(date +%s)-$$"
admin_temp=$(mktemp -d)
export UO_DATA_PATH="$admin_temp/uo"
export MOONGATE_ADMIN_CERT_DIRECTORY="$admin_temp/certs"
export MOONGATE_ADMIN_CERTIFICATE_PASSWORD=""
mkdir -p "$UO_DATA_PATH" "$MOONGATE_ADMIN_CERT_DIRECTORY"
chmod 755 "$admin_temp" "$MOONGATE_ADMIN_CERT_DIRECTORY"
cat > "$admin_temp/no-ports.yaml" <<'YAML'
services:
  login:
    ports: !reset []
  game-1:
    ports: !reset []
  game-2:
    ports: !reset []
YAML
compose=(docker compose -p "$project" -f "$example/compose.yaml" -f "$example/compose.admin.yaml" -f "$admin_temp/no-ports.yaml")
cleanup() {
    "${compose[@]}" down --volumes --remove-orphans >/dev/null 2>&1 || true
    rm -rf "$admin_temp"
}
trap cleanup EXIT
for variable in MOONGATE_POSTGRES_ADMIN_PASSWORD MOONGATE_ACCOUNTS_SCHEMA_PASSWORD MOONGATE_ACCOUNTS_RUNTIME_PASSWORD \
    MOONGATE_REALM_1_SCHEMA_PASSWORD MOONGATE_REALM_1_RUNTIME_PASSWORD MOONGATE_REALM_2_SCHEMA_PASSWORD \
    MOONGATE_REALM_2_RUNTIME_PASSWORD MOONGATE_REDIS_PASSWORD MOONGATE_HANDOFF_SECRET MOONGATE_ADMIN_PASSWORD; do
    export "$variable=$(openssl rand -hex 32)"
done
export MOONGATE_ADMIN_USERNAME=smoke-admin
openssl req -x509 -newkey rsa:2048 -nodes -keyout "$admin_temp/ca.key" -out "$MOONGATE_ADMIN_CERT_DIRECTORY/ca.pem" \
    -subj /CN=Moongate-Smoke-CA -days 1 >/dev/null 2>&1
for service in login game-1 game-2; do
    openssl req -newkey rsa:2048 -nodes -keyout "$admin_temp/$service.key" -out "$admin_temp/$service.csr" \
        -subj "/CN=$service" >/dev/null 2>&1
    printf 'subjectAltName=DNS:%s\nextendedKeyUsage=serverAuth\n' "$service" > "$admin_temp/$service.ext"
    openssl x509 -req -in "$admin_temp/$service.csr" -CA "$MOONGATE_ADMIN_CERT_DIRECTORY/ca.pem" -CAkey "$admin_temp/ca.key" \
        -CAcreateserial -out "$admin_temp/$service.pem" -days 1 -extfile "$admin_temp/$service.ext" >/dev/null 2>&1
    openssl pkcs12 -export -inkey "$admin_temp/$service.key" -in "$admin_temp/$service.pem" \
        -out "$MOONGATE_ADMIN_CERT_DIRECTORY/$service.pfx" -passout pass: >/dev/null 2>&1
    chmod 444 "$MOONGATE_ADMIN_CERT_DIRECTORY/$service.pfx"
done
"${compose[@]}" config --quiet
"${compose[@]}" build login game-1 game-2 auth-schema-apply schema-apply
"${compose[@]}" up -d --wait postgres redis
"${compose[@]}" run --rm auth-schema-apply
"${compose[@]}" run --rm schema-apply
# Seed only this disposable, not-yet-started database. Match the documented PBKDF2 format.
python3 - <<'PY' | "${compose[@]}" exec -T postgres psql -v ON_ERROR_STOP=1 -U moongate_accounts_schema -d moongate_accounts >/dev/null
import os, hashlib, base64, secrets
salt = secrets.token_bytes(16)
hash_bytes = hashlib.pbkdf2_hmac('sha256', os.environ['MOONGATE_ADMIN_PASSWORD'].encode(), salt, 100000, 32)
payload = 'pbkdf2-sha256$100000$' + base64.b64encode(salt).decode() + '$' + base64.b64encode(hash_bytes).decode()
print("INSERT INTO auth.accounts (id,username,hash_password,account_type,created_at,updated_at,is_locked,can_access_api) VALUES (nextval('auth.account_id_seq'),'smoke-admin','" + payload + "',2,now(),now(),false,true);")
PY
"${compose[@]}" up -d login game-1 game-2
for service in login game-1 game-2; do
    ready=false
    for ((attempt=0; attempt<60; attempt++)); do
        if "${compose[@]}" logs "$service" 2>&1 | grep -q 'Moongate Server started.'; then ready=true; break; fi
        sleep 1
    done
    if [[ "$ready" != true ]]; then echo "$service did not start." >&2; "${compose[@]}" logs "$service" >&2; exit 1; fi
done
docker run --rm --network "${project}_moongate" \
    -v "$repository:/source:ro" -v "$MOONGATE_ADMIN_CERT_DIRECTORY:/certs:ro" \
    -e MOONGATE_ADMIN_USERNAME -e MOONGATE_ADMIN_PASSWORD \
    -e MOONGATE_ADMIN_ENDPOINT=https://login:2590 -e MOONGATE_ADMIN_GAME_ENDPOINT=https://game-2:2590 \
    -e MOONGATE_ADMIN_CA=/certs/ca.pem python:3.13-slim sh -ec '
        pip -q install -r /source/samples/admin-python/requirements.txt
        mkdir /tmp/generated
        python -m grpc_tools.protoc -I /source/src/Moongate.Admin.Contracts/proto \
            --python_out=/tmp/generated --grpc_python_out=/tmp/generated \
            /source/src/Moongate.Admin.Contracts/proto/moongate/admin/v1/*.proto
        PYTHONPATH=/tmp/generated python /source/samples/admin-python/client.py
    '
echo 'PASS: published server, private TLS administration, Login/Game sessions and Python client'
