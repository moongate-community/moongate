-- NPC fields rolled from a mobile template; rows saved before get 0 or null.
ALTER TABLE "world"."mobiles"
    ADD COLUMN IF NOT EXISTS "template_id" VARCHAR(255),
    ADD COLUMN IF NOT EXISTS "title" VARCHAR(255),
    ADD COLUMN IF NOT EXISTS "notoriety" INT2,
    ADD COLUMN IF NOT EXISTS "hits" INT4 NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "hits_max" INT4 NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "mana" INT4 NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "mana_max" INT4 NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "stamina" INT4 NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "stamina_max" INT4 NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "fame" INT4 NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "karma" INT4 NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "armor" INT4 NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "resist_physical" INT4 NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "resist_fire" INT4 NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "resist_cold" INT4 NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "resist_poison" INT4 NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "resist_energy" INT4 NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "props" JSONB;

ALTER TABLE "world"."mobiles"
    ALTER COLUMN "hits" DROP DEFAULT,
    ALTER COLUMN "hits_max" DROP DEFAULT,
    ALTER COLUMN "mana" DROP DEFAULT,
    ALTER COLUMN "mana_max" DROP DEFAULT,
    ALTER COLUMN "stamina" DROP DEFAULT,
    ALTER COLUMN "stamina_max" DROP DEFAULT,
    ALTER COLUMN "fame" DROP DEFAULT,
    ALTER COLUMN "karma" DROP DEFAULT,
    ALTER COLUMN "armor" DROP DEFAULT,
    ALTER COLUMN "resist_physical" DROP DEFAULT,
    ALTER COLUMN "resist_fire" DROP DEFAULT,
    ALTER COLUMN "resist_cold" DROP DEFAULT,
    ALTER COLUMN "resist_poison" DROP DEFAULT,
    ALTER COLUMN "resist_energy" DROP DEFAULT;

ALTER TABLE "world"."mobiles" ADD CONSTRAINT "ck_mobiles_notoriety" CHECK ("notoriety" BETWEEN 1 AND 7);

COMMENT ON COLUMN "world"."mobiles"."template_id" IS 'The id of the mobile template an NPC was made from; <see langword="null" /> for a player character.';
COMMENT ON COLUMN "world"."mobiles"."title" IS 'The title when it differs from the template''s; null uses the template''s.';
COMMENT ON COLUMN "world"."mobiles"."notoriety" IS 'The notoriety when it differs from the template''s; null uses the template''s.';
COMMENT ON COLUMN "world"."mobiles"."hits" IS 'The current hit points.';
COMMENT ON COLUMN "world"."mobiles"."hits_max" IS 'The hit points when fully healed.';
COMMENT ON COLUMN "world"."mobiles"."mana" IS 'The current mana.';
COMMENT ON COLUMN "world"."mobiles"."mana_max" IS 'The mana when full.';
COMMENT ON COLUMN "world"."mobiles"."stamina" IS 'The current stamina.';
COMMENT ON COLUMN "world"."mobiles"."stamina_max" IS 'The stamina when fully rested.';
COMMENT ON COLUMN "world"."mobiles"."fame" IS 'The fame; an NPC''s is rolled from its template.';
COMMENT ON COLUMN "world"."mobiles"."karma" IS 'The karma; an NPC''s is rolled from its template.';
COMMENT ON COLUMN "world"."mobiles"."armor" IS 'The armor rating; an NPC''s is rolled from its template.';
COMMENT ON COLUMN "world"."mobiles"."resist_physical" IS 'The physical resistance, in percent.';
COMMENT ON COLUMN "world"."mobiles"."resist_fire" IS 'The fire resistance, in percent.';
COMMENT ON COLUMN "world"."mobiles"."resist_cold" IS 'The cold resistance, in percent.';
COMMENT ON COLUMN "world"."mobiles"."resist_poison" IS 'The poison resistance, in percent.';
COMMENT ON COLUMN "world"."mobiles"."resist_energy" IS 'The energy resistance, in percent.';
