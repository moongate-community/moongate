-- The murder counts of a mobile: the kills a victim reported, and the short-term murders, with the times they lose one. Existing mobiles have none.
ALTER TABLE "world"."mobiles" ADD COLUMN IF NOT EXISTS "kills" INT4 NOT NULL DEFAULT 0;
ALTER TABLE "world"."mobiles" ALTER COLUMN "kills" DROP DEFAULT;
ALTER TABLE "world"."mobiles" ADD COLUMN IF NOT EXISTS "short_term_murders" INT4 NOT NULL DEFAULT 0;
ALTER TABLE "world"."mobiles" ALTER COLUMN "short_term_murders" DROP DEFAULT;
ALTER TABLE "world"."mobiles" ADD COLUMN IF NOT EXISTS "kills_decay_at" TIMESTAMP;
ALTER TABLE "world"."mobiles" ADD COLUMN IF NOT EXISTS "short_term_decay_at" TIMESTAMP;

COMMENT ON COLUMN "world"."mobiles"."kills" IS 'The long-term murder count: the kills a victim reported. From five the player is a murderer and its name is red.';
COMMENT ON COLUMN "world"."mobiles"."short_term_murders" IS 'The short-term murder count, which decays faster than the kills; from five a resurrection costs skills and stats.';
COMMENT ON COLUMN "world"."mobiles"."kills_decay_at" IS 'When the kills lose one, in UTC; null when there are none.';
COMMENT ON COLUMN "world"."mobiles"."short_term_decay_at" IS 'When the short-term murders lose one, in UTC; null when there are none.';
