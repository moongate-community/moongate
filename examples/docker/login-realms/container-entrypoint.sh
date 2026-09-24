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

if [ -n "${MOONGATE_REDIS_PASSWORD_FILE:-}" ]; then
    if [ ! -r "$MOONGATE_REDIS_PASSWORD_FILE" ]; then
        echo "Required Redis secret is not readable: $MOONGATE_REDIS_PASSWORD_FILE" >&2
        exit 1
    fi
    redis_password=$(cat "$MOONGATE_REDIS_PASSWORD_FILE")
    case "$redis_password" in
        ''|*[!0-9a-fA-F]*)
            echo "Redis password must be hexadecimal." >&2
            exit 1
            ;;
    esac
    export MOONGATE_REDIS_CONNECTION_STRING="redis:6379,password=$redis_password"
fi

if [ -n "${MOONGATE_HANDOFF_SECRET_FILE:-}" ]; then
    if [ ! -r "$MOONGATE_HANDOFF_SECRET_FILE" ]; then
        echo "Required handoff secret is not readable: $MOONGATE_HANDOFF_SECRET_FILE" >&2
        exit 1
    fi
    handoff_secret=$(cat "$MOONGATE_HANDOFF_SECRET_FILE")
    case "$handoff_secret" in
        ''|*[!0-9a-fA-F]*)
            echo "Handoff secret must be hexadecimal." >&2
            exit 1
            ;;
    esac
    if [ "${#handoff_secret}" -lt 64 ]; then
        echo "Handoff secret must contain at least 64 hexadecimal characters." >&2
        exit 1
    fi
    export MOONGATE_HANDOFF_SECRET="$handoff_secret"
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
