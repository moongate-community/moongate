-- Reuse the existing allocation sequence without resetting IDs or reservations.
-- A development-generated sequence already attached to the column takes precedence.
DO
$$
BEGIN
    IF
pg_get_serial_sequence('auth.accounts', 'id') IS NULL THEN
ALTER SEQUENCE auth.account_id_seq OWNED BY auth.accounts.id;
END IF;
END
$$;
