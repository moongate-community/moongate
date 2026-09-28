-- The items lying on the ground, read at startup.
CREATE INDEX IF NOT EXISTS ix_items_ground ON world.items (map) WHERE map IS NOT NULL;
