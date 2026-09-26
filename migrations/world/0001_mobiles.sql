CREATE SCHEMA IF NOT EXISTS world;

CREATE SEQUENCE IF NOT EXISTS world.mobiles_id_seq
    AS bigint MINVALUE 1 MAXVALUE 4294967295 START WITH 1 NO CYCLE;

CREATE TABLE IF NOT EXISTS "world"."mobiles" (
  "id" INT8,
  "account_id" INT8,
  "name" VARCHAR(255),
  "gender" INT2 NOT NULL,
  "race" INT2 NOT NULL,
  "body" INT4 NOT NULL,
  "skin_hue" INT4 NOT NULL,
  "strength" INT4 NOT NULL,
  "dexterity" INT4 NOT NULL,
  "intelligence" INT4 NOT NULL,
  "hair_style" INT4 NOT NULL,
  "hair_hue" INT4 NOT NULL,
  "beard_style" INT4 NOT NULL,
  "beard_hue" INT4 NOT NULL,
  "skills" JSONB,
  "created_at" TIMESTAMP NOT NULL,
  "x" INT4 NOT NULL,
  "y" INT4 NOT NULL,
  "z" INT4 NOT NULL,
  "map" INT2 NOT NULL,
  CONSTRAINT "world_mobiles_pkey" PRIMARY KEY ("id")
) WITH (OIDS=FALSE);

ALTER SEQUENCE world.mobiles_id_seq OWNED BY world.mobiles.id;

-- Mobile serials stay in Serial.MinMobile..MaxMobile.
ALTER TABLE world.mobiles
    ADD CONSTRAINT ck_mobiles_id_range CHECK (id BETWEEN 1 AND 1073741823);
