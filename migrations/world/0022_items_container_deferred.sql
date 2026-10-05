-- The check that the container of an item exists can wait for the end of a transaction that asks for it. A world
-- save writes its rows in batches, in no order a container can count on, and a container made after what it holds,
-- such as the corpse of an NPC, could come in a later batch than its contents: the save failed. The save asks for
-- the wait (SET CONSTRAINTS ALL DEFERRED); any other write is still checked at once.
ALTER TABLE "world"."items" ALTER CONSTRAINT "fk_items_container" DEFERRABLE INITIALLY IMMEDIATE;
