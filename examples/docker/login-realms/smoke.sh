#!/usr/bin/env sh
set -eu

example_directory=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repository=$(CDPATH= cd -- "$example_directory/../../.." && pwd)
project="moongate-smoke-$(date +%s)-$$"
temporary_directory=$(mktemp -d)
compose="docker compose -p $project -f $example_directory/compose.yaml"

cleanup()
{
    $compose down --volumes --remove-orphans >/dev/null 2>&1 || true
    rm -rf -- "$temporary_directory"
}
trap cleanup EXIT INT TERM

mkdir -p "$temporary_directory/uo"

if env -i PATH="$PATH" UO_DATA_PATH="$temporary_directory/uo" \
    docker compose -p "$project" -f "$example_directory/compose.yaml" config --quiet >/dev/null 2>&1
then
    echo "Expected Compose validation to reject missing secret inputs." >&2
    exit 1
fi

# Disposable values exist only in this process environment. The Realm 1 values
# contain literal PostgreSQL text-COPY escapes to prove exact password handling.
export UO_DATA_PATH="$temporary_directory/uo"
export MOONGATE_POSTGRES_ADMIN_PASSWORD="smoke-admin-$project"
export MOONGATE_ACCOUNTS_SCHEMA_PASSWORD="smoke-accounts-schema-$project"
export MOONGATE_ACCOUNTS_RUNTIME_PASSWORD="smoke-accounts-runtime-$project"
export MOONGATE_REALM_1_SCHEMA_PASSWORD='smoke-schema-\t-\N-\\-'"$project"
export MOONGATE_REALM_1_RUNTIME_PASSWORD='smoke-runtime-\n-\x41-\\-'"$project"
export MOONGATE_REALM_2_SCHEMA_PASSWORD="smoke-realm-2-schema-$project"
export MOONGATE_REALM_2_RUNTIME_PASSWORD="smoke-realm-2-runtime-$project"

$compose config --quiet
$compose build login game-1 game-2 schema-preview schema-apply
$compose up -d --wait postgres

$compose exec -T -e PGPASSWORD="$MOONGATE_REALM_1_SCHEMA_PASSWORD" postgres \
    psql -h 127.0.0.1 -At -v ON_ERROR_STOP=1 -U moongate_realm_1_schema \
    -d moongate_realm_1 -c "SELECT current_user;" | grep -Fx moongate_realm_1_schema >/dev/null
$compose exec -T -e PGPASSWORD="$MOONGATE_REALM_1_RUNTIME_PASSWORD" postgres \
    psql -h 127.0.0.1 -At -v ON_ERROR_STOP=1 -U moongate_realm_1_runtime \
    -d moongate_realm_1 -c "SELECT current_user;" | grep -Fx moongate_realm_1_runtime >/dev/null
echo "PASS: schema and runtime roles authenticate over TCP with exact secret values"

preview=$($compose run --rm schema-preview)
printf '%s\n' "$preview" | grep -F 'sample_greeter' >/dev/null
printf '%s\n' "$preview" | grep -F 'notes' >/dev/null

$compose run --rm schema-apply
second_preview=$($compose run --rm schema-preview)
printf '%s\n' "$second_preview"
printf '%s\n' "$second_preview" | grep -F 'No PostgreSQL schema changes required.' >/dev/null
echo "PASS: schema preview/apply is idempotent"

postgres_exec="$compose exec -T postgres"
$postgres_exec psql -v ON_ERROR_STOP=1 -U moongate_realm_1_runtime -d moongate_realm_1 \
    -c "INSERT INTO sample_greeter.notes (id, text) VALUES (101, 'seed');" >/dev/null
seed=$($postgres_exec psql -At -v ON_ERROR_STOP=1 -U moongate_realm_1_runtime -d moongate_realm_1 \
    -c "SELECT text FROM sample_greeter.notes WHERE id = 101;")
[ "$seed" = "seed" ] || { echo "Runtime SELECT returned an unexpected value." >&2; exit 1; }
$postgres_exec psql -v ON_ERROR_STOP=1 -U moongate_realm_1_runtime -d moongate_realm_1 \
    -c "UPDATE sample_greeter.notes SET text = 'updated' WHERE id = 101;" >/dev/null
$postgres_exec psql -v ON_ERROR_STOP=1 -U moongate_realm_1_runtime -d moongate_realm_1 \
    -c "INSERT INTO sample_greeter.notes (id, text) VALUES (102, 'delete-me');" >/dev/null
deleted=$($postgres_exec psql -qAt -v ON_ERROR_STOP=1 -U moongate_realm_1_runtime -d moongate_realm_1 \
    -c "DELETE FROM sample_greeter.notes WHERE id = 102 RETURNING id;")
[ "$deleted" = "102" ] || { echo "Runtime DELETE returned an unexpected value." >&2; exit 1; }
echo "PASS: runtime role has SELECT/INSERT/UPDATE/DELETE"

if $postgres_exec psql -v ON_ERROR_STOP=1 -U moongate_realm_1_runtime -d moongate_realm_1 \
    -c "CREATE TABLE sample_greeter.runtime_must_not_create (id bigint);" >/dev/null 2>&1
then
    echo "Realm runtime role unexpectedly performed DDL." >&2
    exit 1
fi
echo "PASS: runtime role cannot perform DDL"

$compose up -d login game-1 game-2
for service in login game-1 game-2
do
    attempts=0
    until $compose logs "$service" 2>&1 | grep -F 'Moongate Server started.' >/dev/null
    do
        attempts=$((attempts + 1))
        if [ "$attempts" -ge 60 ]; then
            $compose logs "$service" >&2
            echo "$service did not start within 60 seconds." >&2
            exit 1
        fi
        sleep 1
    done
done

$compose stop login game-1 game-2
for service in login game-1 game-2
do
    $compose logs "$service" 2>&1 | grep -F 'Moongate Server stopped.' >/dev/null
done

echo "PASS: login-realms schema, privileges, and runtime smoke"
