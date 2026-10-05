-- Why a prisoner was jailed, as the game master typed it.
ALTER TABLE "world"."jail_sentences" ADD COLUMN IF NOT EXISTS "reason" VARCHAR(255);

-- The sentences already there were given without one.
UPDATE "world"."jail_sentences" SET "reason" = '' WHERE "reason" IS NULL;

COMMENT ON COLUMN "world"."jail_sentences"."reason" IS 'Why the prisoner was jailed, as the game master typed it; empty when no reason was given.';
