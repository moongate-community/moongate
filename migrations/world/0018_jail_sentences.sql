-- The jail sentences: who is in which cell until when, and where it goes back.
-- Every entity table has its serial sequence; this one is never drawn from, a row's id is its prisoner's serial.
CREATE SEQUENCE IF NOT EXISTS world.jail_sentences_id_seq
    AS bigint MINVALUE 1 MAXVALUE 4294967295 START WITH 1 NO CYCLE;

CREATE TABLE IF NOT EXISTS "world"."jail_sentences" (
  "id" INT8,
  "name" VARCHAR(255),
  "is_player" BOOL NOT NULL,
  "cell" INT4 NOT NULL,
  "days" INT4 NOT NULL,
  "jailed_at" INT8 NOT NULL,
  "release_at" INT8 NOT NULL,
  "return_map" INT2 NOT NULL,
  "return_x" INT4 NOT NULL,
  "return_y" INT4 NOT NULL,
  "return_z" INT4 NOT NULL,
  "jailed_by" VARCHAR(255),
  "pardoned" BOOL NOT NULL,
  CONSTRAINT "world_jail_sentences_pkey" PRIMARY KEY ("id")
) WITH (OIDS=FALSE);

ALTER SEQUENCE world.jail_sentences_id_seq OWNED BY world.jail_sentences.id;

COMMENT ON TABLE "world"."jail_sentences" IS 'A jail sentence: who is in which cell until when, and where it goes back.';
COMMENT ON COLUMN "world"."jail_sentences"."id" IS 'The serial of the prisoner: a mobile has one sentence at most.';
COMMENT ON COLUMN "world"."jail_sentences"."name" IS 'The name of the prisoner when it was jailed.';
COMMENT ON COLUMN "world"."jail_sentences"."is_player" IS 'Whether the prisoner is the character of a player: one that is not in the world is offline, not gone.';
COMMENT ON COLUMN "world"."jail_sentences"."cell" IS 'The number of the cell.';
COMMENT ON COLUMN "world"."jail_sentences"."days" IS 'The length of the sentence in days.';
COMMENT ON COLUMN "world"."jail_sentences"."jailed_at" IS 'When the sentence began, in Unix milliseconds.';
COMMENT ON COLUMN "world"."jail_sentences"."release_at" IS 'When the sentence ends, in Unix milliseconds.';
COMMENT ON COLUMN "world"."jail_sentences"."return_map" IS 'The map of the place of the arrest, stored as its byte value.';
COMMENT ON COLUMN "world"."jail_sentences"."return_x" IS 'The X of the place of the arrest, where the prisoner goes back.';
COMMENT ON COLUMN "world"."jail_sentences"."return_y" IS 'The Y of the place of the arrest.';
COMMENT ON COLUMN "world"."jail_sentences"."return_z" IS 'The Z of the place of the arrest.';
COMMENT ON COLUMN "world"."jail_sentences"."jailed_by" IS 'The name of the game master who jailed.';
COMMENT ON COLUMN "world"."jail_sentences"."pardoned" IS 'Whether the staff ended the sentence early: no fine and no note at the release.';
