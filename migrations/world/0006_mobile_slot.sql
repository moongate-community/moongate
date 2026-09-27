-- The character-list slot of a player character.
ALTER TABLE "world"."mobiles" ADD COLUMN IF NOT EXISTS "slot" INT2;

ALTER TABLE "world"."mobiles" ADD CONSTRAINT "ck_mobiles_slot" CHECK ("slot" BETWEEN 0 AND 6);

-- NULLs are distinct, so NPCs (no account, no slot) never collide.
CREATE UNIQUE INDEX IF NOT EXISTS ux_mobiles_account_slot ON world.mobiles (account_id, slot);

COMMENT ON COLUMN "world"."mobiles"."slot" IS 'The character-list slot of a player character; <see langword="null" /> for an NPC.';
