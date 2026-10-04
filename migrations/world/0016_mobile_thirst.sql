-- How quenched a mobile is, from 0 (parched) to 20 (quenched); existing mobiles are quenched.
ALTER TABLE "world"."mobiles" ADD COLUMN IF NOT EXISTS "thirst" INT4 NOT NULL DEFAULT 20;
ALTER TABLE "world"."mobiles" ALTER COLUMN "thirst" DROP DEFAULT;

COMMENT ON COLUMN "world"."mobiles"."thirst" IS 'How quenched the mobile is, from 0 (parched) to 20: it drops with time for a player and rises by drinking.';
