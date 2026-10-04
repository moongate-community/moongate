-- How full a mobile is, from 0 (starving) to 20 (full); existing mobiles are full.
ALTER TABLE "world"."mobiles" ADD COLUMN IF NOT EXISTS "hunger" INT4 NOT NULL DEFAULT 20;
ALTER TABLE "world"."mobiles" ALTER COLUMN "hunger" DROP DEFAULT;

COMMENT ON COLUMN "world"."mobiles"."hunger" IS 'How full the mobile is, from 0 (starving) to 20 (full): it drops with time for a player and rises by eating.';
