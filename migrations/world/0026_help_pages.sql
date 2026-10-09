-- The requests for a game master: who asked, what about, where, and how the staff dealt with it.
-- Every entity table has its serial sequence; this one is never drawn from, the service counts up from the highest id.
CREATE SEQUENCE IF NOT EXISTS world.help_pages_id_seq
    AS bigint MINVALUE 1 MAXVALUE 4294967295 START WITH 1 NO CYCLE;

CREATE TABLE IF NOT EXISTS "world"."help_pages" (
  "id" INT8,
  "player" INT8 NOT NULL,
  "player_name" VARCHAR(255),
  "account_id" INT8 NOT NULL,
  "kind" INT2 NOT NULL,
  "text" VARCHAR(255),
  "map" INT2 NOT NULL,
  "x" INT4 NOT NULL,
  "y" INT4 NOT NULL,
  "z" INT4 NOT NULL,
  "status" INT2 NOT NULL,
  "taken_by" VARCHAR(255),
  "answer" VARCHAR(255),
  "answer_delivered" BOOL NOT NULL DEFAULT false,
  "created_at" INT8 NOT NULL,
  "closed_at" INT8 NOT NULL,
  CONSTRAINT "world_help_pages_pkey" PRIMARY KEY ("id")
) WITH (OIDS=FALSE);

ALTER SEQUENCE world.help_pages_id_seq OWNED BY world.help_pages.id;

COMMENT ON TABLE "world"."help_pages" IS 'A request for a game master: who asked, what about, where, and how the staff dealt with it.';
COMMENT ON COLUMN "world"."help_pages"."id" IS 'The number of the request, shown to the staff.';
COMMENT ON COLUMN "world"."help_pages"."player" IS 'The serial of the character who asked.';
COMMENT ON COLUMN "world"."help_pages"."player_name" IS 'The name of the character when it asked.';
COMMENT ON COLUMN "world"."help_pages"."account_id" IS 'The serial of the account of the character.';
COMMENT ON COLUMN "world"."help_pages"."kind" IS 'What it is about, stored as the byte value of HelpPageKindType: 0 question, 1 bug, 2 suggestion, 3 harassment.';
COMMENT ON COLUMN "world"."help_pages"."text" IS 'The line the player typed, at most 128 characters.';
COMMENT ON COLUMN "world"."help_pages"."map" IS 'The map where the player stood, stored as its byte value.';
COMMENT ON COLUMN "world"."help_pages"."x" IS 'The X where the player stood.';
COMMENT ON COLUMN "world"."help_pages"."y" IS 'The Y where the player stood.';
COMMENT ON COLUMN "world"."help_pages"."z" IS 'The Z where the player stood.';
COMMENT ON COLUMN "world"."help_pages"."status" IS 'Stored as the byte value of HelpPageStatusType: 0 open, 1 taken, 2 closed.';
COMMENT ON COLUMN "world"."help_pages"."taken_by" IS 'The name of the game master who took or closed it; empty when nobody did.';
COMMENT ON COLUMN "world"."help_pages"."answer" IS 'The answer of the game master, at most 128 characters; empty when it was closed without one.';
COMMENT ON COLUMN "world"."help_pages"."answer_delivered" IS 'Whether the player has read the answer; true for a request closed without one.';
COMMENT ON COLUMN "world"."help_pages"."created_at" IS 'When it was asked, in Unix milliseconds.';
COMMENT ON COLUMN "world"."help_pages"."closed_at" IS 'When it was closed, in Unix milliseconds; 0 while it is not.';
