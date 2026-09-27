-- FreeSql writes an entity's XML docs as table and column comments and compares them at startup, so the world
-- tables carry the docs of ItemEntity and MobileEntity word for word; without them the server refuses to start.
-- Changing one of those docs needs a migration like this one.
COMMENT ON COLUMN "world"."items"."template_id" IS 'The id of the item template the item was made from, such as <c>orcspawn</c>.';
COMMENT ON COLUMN "world"."items"."item_id" IS 'The graphic; it can differ from the template''s, as for an opened door.';
COMMENT ON COLUMN "world"."items"."name" IS 'The name when it differs from the template''s; null uses the template''s or tiledata''s name.';
COMMENT ON COLUMN "world"."items"."map" IS 'The <see cref="T:Moongate.Ultima.Types.MapType" /> value when the item is on the ground.';
COMMENT ON COLUMN "world"."items"."container_id" IS 'The container item holding this item.';
COMMENT ON COLUMN "world"."items"."mobile_id" IS 'The mobile wearing this item.';
COMMENT ON COLUMN "world"."items"."layer" IS 'The <see cref="T:Moongate.Ultima.Types.LayerType" /> value when the item is worn.';
COMMENT ON COLUMN "world"."items"."movable" IS 'Whether the item can be picked up, when it differs from the template''s.';
COMMENT ON COLUMN "world"."items"."visibility" IS 'The <see cref="T:Moongate.Server.Core.Types.Accounts.AccountType" /> value that sees the item, when it differs from the template''s.';
COMMENT ON COLUMN "world"."items"."decay_at" IS 'When the item decays, in UTC; null never decays.';
COMMENT ON TABLE "world"."items" IS 'An item of the world: on the ground, inside a container item or worn by a mobile, never in two places.';

COMMENT ON COLUMN "world"."mobiles"."account_id" IS 'The account of a player character; <see langword="null" /> for an NPC.';
COMMENT ON COLUMN "world"."mobiles"."body" IS 'The body id, which follows race and gender (for example 400 for a male human).';
COMMENT ON COLUMN "world"."mobiles"."hair_style" IS 'The item id of the hair style; 0 means no hair.';
COMMENT ON COLUMN "world"."mobiles"."beard_style" IS 'The item id of the beard style; 0 means no beard.';
COMMENT ON COLUMN "world"."mobiles"."skills" IS 'The skills the mobile has; a skill missing from the list is at 0 with the default cap and lock.';
COMMENT ON COLUMN "world"."mobiles"."map" IS 'Stored as its byte value: <see cref="T:Moongate.Ultima.Types.MapType" /> is byte-backed, which FreeSql cannot write to an int column.';
COMMENT ON TABLE "world"."mobiles" IS 'A mobile of the world: a player character or an NPC. Both share the same serial range and the same state; an
                NPC is the one without an <see cref="P:Moongate.Server.Ultima.Entities.World.MobileEntity.AccountId" />.';
