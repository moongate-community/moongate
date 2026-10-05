-- The messages of the bulletin boards: who wrote what on which board, and the thread each belongs to.
-- Every entity table has its serial sequence; this one is never drawn from, a message takes an item serial.
CREATE SEQUENCE IF NOT EXISTS world.bulletin_messages_id_seq
    AS bigint MINVALUE 1 MAXVALUE 4294967295 START WITH 1 NO CYCLE;

CREATE TABLE IF NOT EXISTS "world"."bulletin_messages" (
  "id" INT8,
  "board_id" INT8 NOT NULL,
  "thread_id" INT8 NOT NULL,
  "poster_id" INT8 NOT NULL,
  "poster_name" VARCHAR(255),
  "subject" VARCHAR(255),
  "body" TEXT,
  "posted_at" INT8 NOT NULL,
  "last_reply_at" INT8 NOT NULL,
  "poster_body" INT4 NOT NULL,
  "poster_hue" INT4 NOT NULL,
  "poster_equipment" TEXT,
  CONSTRAINT "world_bulletin_messages_pkey" PRIMARY KEY ("id")
) WITH (OIDS=FALSE);

ALTER SEQUENCE world.bulletin_messages_id_seq OWNED BY world.bulletin_messages.id;

CREATE INDEX IF NOT EXISTS "ix_bulletin_messages_board" ON "world"."bulletin_messages" ("board_id");

COMMENT ON TABLE "world"."bulletin_messages" IS 'A message on a bulletin board: who wrote what on which board, and the thread it belongs to.';
COMMENT ON COLUMN "world"."bulletin_messages"."id" IS 'The serial of the message, taken from the item serials: the client lists it as an item of the board.';
COMMENT ON COLUMN "world"."bulletin_messages"."board_id" IS 'The serial of the board item the message is on.';
COMMENT ON COLUMN "world"."bulletin_messages"."thread_id" IS 'The serial of the first message of the thread; 0 on that first message.';
COMMENT ON COLUMN "world"."bulletin_messages"."poster_id" IS 'The serial of the character who posted; 0 when a script did.';
COMMENT ON COLUMN "world"."bulletin_messages"."poster_name" IS 'The name of the poster when it posted.';
COMMENT ON COLUMN "world"."bulletin_messages"."subject" IS 'The subject, one line.';
COMMENT ON COLUMN "world"."bulletin_messages"."body" IS 'The lines of the message, joined with a line feed.';
COMMENT ON COLUMN "world"."bulletin_messages"."posted_at" IS 'When it was posted, in Unix milliseconds.';
COMMENT ON COLUMN "world"."bulletin_messages"."last_reply_at" IS 'On the first message of a thread: when it or its last reply was posted, in Unix milliseconds.';
COMMENT ON COLUMN "world"."bulletin_messages"."poster_body" IS 'The body of the poster when it posted.';
COMMENT ON COLUMN "world"."bulletin_messages"."poster_hue" IS 'The hue of the poster when it posted.';
COMMENT ON COLUMN "world"."bulletin_messages"."poster_equipment" IS 'What the poster wore when it posted: itemId:hue pairs joined with commas.';
