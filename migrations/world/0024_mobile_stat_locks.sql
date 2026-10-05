-- Which way each stat of a mobile may move: 0 up, 1 down, 2 locked. Existing mobiles are all up.
ALTER TABLE "world"."mobiles" ADD COLUMN IF NOT EXISTS "str_lock" INT2 NOT NULL DEFAULT 0;
ALTER TABLE "world"."mobiles" ALTER COLUMN "str_lock" DROP DEFAULT;
ALTER TABLE "world"."mobiles" ADD COLUMN IF NOT EXISTS "dex_lock" INT2 NOT NULL DEFAULT 0;
ALTER TABLE "world"."mobiles" ALTER COLUMN "dex_lock" DROP DEFAULT;
ALTER TABLE "world"."mobiles" ADD COLUMN IF NOT EXISTS "int_lock" INT2 NOT NULL DEFAULT 0;
ALTER TABLE "world"."mobiles" ALTER COLUMN "int_lock" DROP DEFAULT;

COMMENT ON COLUMN "world"."mobiles"."str_lock" IS 'Which way the strength may move: 0 up, 1 down, 2 locked.';
COMMENT ON COLUMN "world"."mobiles"."dex_lock" IS 'Which way the dexterity may move: 0 up, 1 down, 2 locked.';
COMMENT ON COLUMN "world"."mobiles"."int_lock" IS 'Which way the intelligence may move: 0 up, 1 down, 2 locked.';
