-- Where a mobile faces; existing mobiles face south.
ALTER TABLE "world"."mobiles" ADD COLUMN IF NOT EXISTS "direction" INT2 NOT NULL DEFAULT 4;
ALTER TABLE "world"."mobiles" ALTER COLUMN "direction" DROP DEFAULT;
ALTER TABLE "world"."mobiles" ADD CONSTRAINT "ck_mobiles_direction" CHECK ("direction" BETWEEN 0 AND 7);

COMMENT ON COLUMN "world"."mobiles"."direction" IS 'Where the mobile faces, without the running bit; a character comes back facing the same way.';
