-- The slot of an item in the grid the Enhanced Client shows a container as (0 to 124). Items already in a container
-- are numbered in id order; a container with more items than slots shares the last one.
ALTER TABLE "world"."items" ADD COLUMN IF NOT EXISTS "grid_index" INT2;

UPDATE world.items AS item
SET grid_index = LEAST(numbered.slot, 124)
FROM (
    SELECT id, ROW_NUMBER() OVER (PARTITION BY container_id ORDER BY id) - 1 AS slot
    FROM world.items
    WHERE container_id IS NOT NULL
) AS numbered
WHERE item.id = numbered.id AND item.grid_index IS NULL;

ALTER TABLE world.items DROP CONSTRAINT IF EXISTS ck_items_grid_index;
ALTER TABLE world.items
    ADD CONSTRAINT ck_items_grid_index CHECK (
        (container_id IS NULL) = (grid_index IS NULL) AND (grid_index IS NULL OR grid_index BETWEEN 0 AND 124)
    );

COMMENT ON COLUMN "world"."items"."grid_index" IS 'The slot (0 to 124) in the grid the Enhanced Client shows a container as; the classic client ignores it.';
