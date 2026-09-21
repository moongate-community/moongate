CREATE SCHEMA IF NOT EXISTS sample_greeter;
CREATE TABLE IF NOT EXISTS sample_greeter.notes (
    id bigint PRIMARY KEY,
    text varchar(200)
);
COMMENT ON TABLE sample_greeter.notes IS 'A plugin-owned row demonstrating stable attribute mappings and application-assigned identities.';
