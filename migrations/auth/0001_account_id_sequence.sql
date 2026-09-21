-- Account IDs are independent from the UO mobile/item serial ranges.
-- Run migrations before starting writers. Existing accounts retain their IDs.
CREATE SCHEMA IF NOT EXISTS auth;
CREATE SEQUENCE IF NOT EXISTS auth.account_id_seq
    AS bigint MINVALUE 1 MAXVALUE 4294967295 START WITH 1 NO CYCLE;

DO $$
DECLARE
    highest_id bigint;
    sequence_value bigint;
BEGIN
    IF to_regclass('auth.accounts') IS NOT NULL THEN
        LOCK TABLE auth.accounts IN ACCESS EXCLUSIVE MODE;
        SELECT MAX(id) INTO highest_id FROM auth.accounts;
        SELECT last_value INTO sequence_value FROM auth.account_id_seq;
        IF highest_id IS NOT NULL AND highest_id >= sequence_value THEN
            PERFORM setval('auth.account_id_seq', highest_id, true);
        END IF;
    END IF;
END
$$;
