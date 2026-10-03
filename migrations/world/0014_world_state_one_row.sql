-- The world state is one row: a second one would never be read.
ALTER TABLE world.state DROP CONSTRAINT IF EXISTS ck_state_one_row;
ALTER TABLE world.state
    ADD CONSTRAINT ck_state_one_row CHECK (id = 1);
