-- Allocate IDs without requiring plugin services to know the sequence name.
DO $$
DECLARE highest_id bigint;
BEGIN
    IF pg_get_serial_sequence('sample_greeter.notes', 'id') IS NULL THEN
        LOCK TABLE sample_greeter.notes IN SHARE ROW EXCLUSIVE MODE;
        SELECT COALESCE(MAX(id), 0) INTO highest_id FROM sample_greeter.notes;
        IF highest_id < 0 OR highest_id > 4294967295 THEN
            RAISE EXCEPTION 'Existing identity is outside the Serial range';
        END IF;
        CREATE SEQUENCE sample_greeter.notes_id_seq
            AS bigint MINVALUE 1 MAXVALUE 4294967295 START WITH 1 NO CYCLE;
        ALTER SEQUENCE sample_greeter.notes_id_seq OWNED BY sample_greeter.notes.id;
        PERFORM setval('sample_greeter.notes_id_seq', GREATEST(highest_id, 1), highest_id > 0);
    END IF;
END
$$;
