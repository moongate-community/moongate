-- The runtime requires MINVALUE 1 and MAXVALUE 4294967295, so the item range starts through START WITH
-- (Serial.MinItem) and its top is held by ck_items_id_range.
CREATE SEQUENCE IF NOT EXISTS world.items_id_seq
    AS bigint MINVALUE 1 MAXVALUE 4294967295 START WITH 1073741824 NO CYCLE;

CREATE TABLE IF NOT EXISTS "world"."items" (
  "id" INT8,
  "template_id" VARCHAR(255),
  "item_id" INT4 NOT NULL,
  "hue" INT4 NOT NULL,
  "amount" INT4 NOT NULL,
  "name" VARCHAR(255),
  "map" INT2,
  "x" INT4,
  "y" INT4,
  "z" INT2,
  "container_id" INT8,
  "grid_x" INT2,
  "grid_y" INT2,
  "mobile_id" INT8,
  "layer" INT2,
  "movable" BOOL,
  "visibility" INT2,
  "decay_at" TIMESTAMP,
  "props" JSONB,
  CONSTRAINT "world_items_pkey" PRIMARY KEY ("id")
) WITH (OIDS=FALSE);

ALTER SEQUENCE world.items_id_seq OWNED BY world.items.id;

-- An item is on the ground, in a container item or worn by a mobile: exactly one, never itself, and deleting a
-- container or a mobile deletes what it holds.
ALTER TABLE world.items
    ADD CONSTRAINT ck_items_id_range CHECK (id BETWEEN 1073741824 AND 2129587950),
    ADD CONSTRAINT ck_items_amount CHECK (amount >= 1),
    ADD CONSTRAINT ck_items_one_location CHECK (num_nonnulls(map, container_id, mobile_id) = 1),
    ADD CONSTRAINT ck_items_ground CHECK ((map IS NULL) = (x IS NULL) AND (map IS NULL) = (y IS NULL) AND (map IS NULL) = (z IS NULL)),
    ADD CONSTRAINT ck_items_container CHECK ((container_id IS NULL) = (grid_x IS NULL) AND (container_id IS NULL) = (grid_y IS NULL)),
    ADD CONSTRAINT ck_items_worn CHECK ((mobile_id IS NULL) = (layer IS NULL)),
    ADD CONSTRAINT ck_items_not_self_contained CHECK (container_id IS NULL OR container_id <> id),
    ADD CONSTRAINT fk_items_container FOREIGN KEY (container_id) REFERENCES world.items (id) ON DELETE CASCADE,
    ADD CONSTRAINT fk_items_mobile FOREIGN KEY (mobile_id) REFERENCES world.mobiles (id) ON DELETE CASCADE;

CREATE INDEX IF NOT EXISTS ix_items_container_id ON world.items (container_id) WHERE container_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_items_mobile_id ON world.items (mobile_id) WHERE mobile_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_items_mobile_layer ON world.items (mobile_id, layer) WHERE mobile_id IS NOT NULL;
