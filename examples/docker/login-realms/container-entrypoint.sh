#!/usr/bin/env sh
set -eu

percent_encode()
{
    od -An -v -tx1 | tr -d ' \n' | sed 's/../%&/g'
}

export_connection()
{
    variable_name=$1
    database_name=$2
    user_name=$3
    secret_file=$4
    if [ ! -r "$secret_file" ]; then
        echo "Required database secret is not readable: $secret_file" >&2
        exit 1
    fi
    password=$(percent_encode < "$secret_file")
    database=$(printf '%s' "$database_name" | percent_encode)
    user=$(printf '%s' "$user_name" | percent_encode)
    host=${MOONGATE_DATABASE_HOST:-postgres}
    case "$host" in
        \[*\]) ;;
        *:*) host="[$host]" ;;
    esac
    port=${MOONGATE_DATABASE_PORT:-5432}
    export "$variable_name=postgres://$user:$password@$host:$port/$database?pooling=true"
}

if [ -n "${MOONGATE_ACCOUNTS_DATABASE_FILE:-}" ]; then
    export_connection MOONGATE_ACCOUNTS_DATABASE \
        "${MOONGATE_ACCOUNTS_DATABASE_NAME:?Set MOONGATE_ACCOUNTS_DATABASE_NAME}" \
        "${MOONGATE_ACCOUNTS_RUNTIME_USER:?Set MOONGATE_ACCOUNTS_RUNTIME_USER}" \
        "$MOONGATE_ACCOUNTS_DATABASE_FILE"
fi

if [ -n "${MOONGATE_ACCOUNTS_SCHEMA_DATABASE_FILE:-}" ]; then
    export_connection MOONGATE_ACCOUNTS_SCHEMA_DATABASE \
        "${MOONGATE_ACCOUNTS_DATABASE_NAME:?Set MOONGATE_ACCOUNTS_DATABASE_NAME}" \
        "${MOONGATE_ACCOUNTS_SCHEMA_USER:?Set MOONGATE_ACCOUNTS_SCHEMA_USER}" \
        "$MOONGATE_ACCOUNTS_SCHEMA_DATABASE_FILE"
fi

if [ -n "${MOONGATE_REALM_DATABASE_FILE:-}" ]; then
    export_connection MOONGATE_REALM_DATABASE \
        "${MOONGATE_REALM_DATABASE_NAME:?Set MOONGATE_REALM_DATABASE_NAME}" \
        "${MOONGATE_REALM_RUNTIME_USER:?Set MOONGATE_REALM_RUNTIME_USER}" \
        "$MOONGATE_REALM_DATABASE_FILE"
fi

if [ -n "${MOONGATE_REALM_SCHEMA_DATABASE_FILE:-}" ]; then
    export_connection MOONGATE_REALM_SCHEMA_DATABASE \
        "${MOONGATE_REALM_DATABASE_NAME:?Set MOONGATE_REALM_DATABASE_NAME}" \
        "${MOONGATE_REALM_SCHEMA_USER:?Set MOONGATE_REALM_SCHEMA_USER}" \
        "$MOONGATE_REALM_SCHEMA_DATABASE_FILE"
fi

if [ -d /opt/moongate/sample-plugin ]; then
    mkdir -p /data/plugins/SamplePlugin
    cp -a /opt/moongate/sample-plugin/. /data/plugins/SamplePlugin/
fi

if [ "${1:-}" = "migrations" ]; then
    shift
    exec /app/migration-runner/Moongate.MigrationRunner "$@"
fi

exec /app/Moongate.Server "$@"
