-- What the shard as a whole keeps across restarts: one row, with the props scripts set with world.set_prop.
-- Every entity table has its serial sequence; this one is never drawn from, the row's id is fixed.
CREATE SEQUENCE IF NOT EXISTS world.state_id_seq
    AS bigint MINVALUE 1 MAXVALUE 4294967295 START WITH 1 NO CYCLE;

CREATE TABLE IF NOT EXISTS "world"."state" (
  "id" INT8,
  "props" JSONB,
  CONSTRAINT "world_state_pkey" PRIMARY KEY ("id")
) WITH (OIDS=FALSE);

ALTER SEQUENCE world.state_id_seq OWNED BY world.state.id;

COMMENT ON TABLE "world"."state" IS 'What the shard as a whole keeps across restarts: one row with the props scripts set with world.set_prop.';
COMMENT ON COLUMN "world"."state"."id" IS 'The id of the one row.';
COMMENT ON COLUMN "world"."state"."props" IS 'The values scripts keep for the whole shard: strings, numbers and bools by key.';
