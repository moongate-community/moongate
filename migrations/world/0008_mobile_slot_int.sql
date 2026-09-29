-- The character-list slot is an int on the entity; widen the column to match.
ALTER TABLE "world"."mobiles" ALTER COLUMN "slot" TYPE INT4;
