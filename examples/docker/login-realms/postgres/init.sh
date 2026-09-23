#!/usr/bin/env sh
set -eu

provision_role()
{
    role_name=$1
    secret_file=$2
    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
        --set=role_name="$role_name" <<SQL
SELECT format('CREATE ROLE %I LOGIN', :'role_name')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = :'role_name') \gexec
SELECT format('ALTER ROLE %I PASSWORD %L', :'role_name', pg_read_file('$secret_file')) \gexec
SQL
}

provision_database()
{
    database_name=$1
    schema_role=$2
    runtime_role=$3
    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
        --set=database_name="$database_name" --set=schema_role="$schema_role" <<'SQL'
SELECT format('CREATE DATABASE %I OWNER %I', :'database_name', :'schema_role')
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = :'database_name') \gexec
SQL
    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$database_name" \
        --set=database_name="$database_name" --set=schema_role="$schema_role" \
        --set=runtime_role="$runtime_role" <<'SQL'
SELECT format('REVOKE ALL ON DATABASE %I FROM PUBLIC', :'database_name') \gexec
SELECT format('GRANT CONNECT ON DATABASE %I TO %I', :'database_name', :'runtime_role') \gexec
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
SELECT format('CREATE SCHEMA IF NOT EXISTS moongate_migrations AUTHORIZATION %I', :'schema_role') \gexec
REVOKE ALL ON SCHEMA moongate_migrations FROM PUBLIC;
SELECT format('GRANT USAGE ON SCHEMA moongate_migrations TO %I', :'runtime_role') \gexec
SELECT format('GRANT SELECT ON ALL TABLES IN SCHEMA moongate_migrations TO %I', :'runtime_role') \gexec
SELECT format('ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA moongate_migrations GRANT SELECT ON TABLES TO %I', :'schema_role', :'runtime_role') \gexec
SQL
}

provision_sample_schema()
{
    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$MOONGATE_REALM_1_DATABASE" \
        --set=schema_role="$MOONGATE_REALM_1_SCHEMA_USER" \
        --set=runtime_role="$MOONGATE_REALM_1_RUNTIME_USER" <<'SQL'
SELECT format('CREATE SCHEMA IF NOT EXISTS sample_greeter AUTHORIZATION %I', :'schema_role') \gexec
REVOKE ALL ON SCHEMA sample_greeter FROM PUBLIC;
SELECT format('GRANT USAGE ON SCHEMA sample_greeter TO %I', :'runtime_role') \gexec
SELECT format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA sample_greeter TO %I', :'runtime_role') \gexec
SELECT format('ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA sample_greeter GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I', :'schema_role', :'runtime_role') \gexec
SQL
}

provision_auth_schema()
{
    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$MOONGATE_ACCOUNTS_DATABASE" \
        --set=schema_role="$MOONGATE_ACCOUNTS_SCHEMA_USER" \
        --set=runtime_role="$MOONGATE_ACCOUNTS_RUNTIME_USER" <<'SQL'
SELECT format('CREATE SCHEMA IF NOT EXISTS auth AUTHORIZATION %I', :'schema_role') \gexec
REVOKE ALL ON SCHEMA auth FROM PUBLIC;
SELECT format('GRANT USAGE ON SCHEMA auth TO %I', :'runtime_role') \gexec
SELECT format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA auth TO %I', :'runtime_role') \gexec
SELECT format('GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA auth TO %I', :'runtime_role') \gexec
SELECT format('ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA auth GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I', :'schema_role', :'runtime_role') \gexec
SELECT format('ALTER DEFAULT PRIVILEGES FOR ROLE %I IN SCHEMA auth GRANT USAGE, SELECT ON SEQUENCES TO %I', :'schema_role', :'runtime_role') \gexec
SQL
}

provision_role "$MOONGATE_ACCOUNTS_SCHEMA_USER" /run/secrets/accounts-schema-password
provision_role "$MOONGATE_ACCOUNTS_RUNTIME_USER" /run/secrets/accounts-runtime-password
provision_role "$MOONGATE_REALM_1_SCHEMA_USER" /run/secrets/realm-1-schema-password
provision_role "$MOONGATE_REALM_1_RUNTIME_USER" /run/secrets/realm-1-runtime-password
provision_role "$MOONGATE_REALM_2_SCHEMA_USER" /run/secrets/realm-2-schema-password
provision_role "$MOONGATE_REALM_2_RUNTIME_USER" /run/secrets/realm-2-runtime-password

provision_database "$MOONGATE_ACCOUNTS_DATABASE" "$MOONGATE_ACCOUNTS_SCHEMA_USER" "$MOONGATE_ACCOUNTS_RUNTIME_USER"
provision_database "$MOONGATE_REALM_1_DATABASE" "$MOONGATE_REALM_1_SCHEMA_USER" "$MOONGATE_REALM_1_RUNTIME_USER"
provision_database "$MOONGATE_REALM_2_DATABASE" "$MOONGATE_REALM_2_SCHEMA_USER" "$MOONGATE_REALM_2_RUNTIME_USER"
provision_auth_schema
provision_sample_schema
