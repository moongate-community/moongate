-- Whether a mobile is hidden from the players and whether it is frozen in place; existing mobiles are neither.
ALTER TABLE "world"."mobiles" ADD COLUMN IF NOT EXISTS "hidden" BOOL NOT NULL DEFAULT false;
ALTER TABLE "world"."mobiles" ALTER COLUMN "hidden" DROP DEFAULT;
ALTER TABLE "world"."mobiles" ADD COLUMN IF NOT EXISTS "frozen" BOOL NOT NULL DEFAULT false;
ALTER TABLE "world"."mobiles" ALTER COLUMN "frozen" DROP DEFAULT;

COMMENT ON COLUMN "world"."mobiles"."hidden" IS 'Whether the mobile is hidden: the players do not see it, the staff does.';
COMMENT ON COLUMN "world"."mobiles"."frozen" IS 'Whether the mobile is frozen: it neither steps nor turns.';
