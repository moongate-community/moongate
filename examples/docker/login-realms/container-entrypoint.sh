#!/usr/bin/env sh
set -eu

read_password()
{
    secret_file=$1
    if [ ! -r "$secret_file" ]; then
        echo "Required database secret is not readable: $secret_file" >&2
        exit 1
    fi
    cat -- "$secret_file"
}

quote_connection_value()
{
    printf '%s' "$1" | sed 's/"/""/g'
}

export_connection()
{
    variable_name=$1
    database_name=$2
    user_name=$3
    secret_file=$4
    password=$(quote_connection_value "$(read_password "$secret_file")")
    host=$(quote_connection_value "${MOONGATE_DATABASE_HOST:-postgres}")
    database=$(quote_connection_value "$database_name")
    user=$(quote_connection_value "$user_name")
    port=${MOONGATE_DATABASE_PORT:-5432}
    export "$variable_name=Host=\"$host\";Port=$port;Database=\"$database\";Username=\"$user\";Password=\"$password\";Pooling=true"
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

exec /app/Moongate.Server "$@"
