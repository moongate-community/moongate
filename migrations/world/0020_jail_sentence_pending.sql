-- Whether a sentence waits for its prisoner, a player who was offline, to log in.
ALTER TABLE "world"."jail_sentences" ADD COLUMN IF NOT EXISTS "pending" BOOLEAN NOT NULL DEFAULT false;

COMMENT ON COLUMN "world"."jail_sentences"."pending" IS 'Whether the sentence waits for its prisoner, a player who was offline, to log in: its days start then.';
