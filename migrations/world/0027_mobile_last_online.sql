-- When a character last left the world, for the staff. Existing characters have none until their next logout.
ALTER TABLE "world"."mobiles" ADD COLUMN IF NOT EXISTS "last_online_at" TIMESTAMP;

COMMENT ON COLUMN "world"."mobiles"."last_online_at" IS 'When the character last left the world, in UTC; null until its first logout after this was recorded.';
