-- Items keep the rarity picked when they were made; rows saved before get Common (0).
ALTER TABLE "world"."items" ADD COLUMN IF NOT EXISTS "rarity" INT2 NOT NULL DEFAULT 0;
ALTER TABLE "world"."items" ALTER COLUMN "rarity" DROP DEFAULT;

ALTER TABLE "world"."items" ADD CONSTRAINT "ck_items_rarity" CHECK ("rarity" BETWEEN 0 AND 4);

COMMENT ON COLUMN "world"."items"."rarity" IS 'The rarity picked when the item was made.';
