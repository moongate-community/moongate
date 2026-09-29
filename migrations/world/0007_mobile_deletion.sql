-- When the player asked to delete a character; the character is removed later.
ALTER TABLE "world"."mobiles" ADD COLUMN IF NOT EXISTS "deletion_requested_at" TIMESTAMP;

COMMENT ON COLUMN "world"."mobiles"."deletion_requested_at" IS 'When the player asked to delete this character, in UTC; null for an active character or an NPC.';
