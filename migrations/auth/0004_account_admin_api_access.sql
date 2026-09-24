ALTER TABLE auth.accounts
    ADD COLUMN IF NOT EXISTS can_access_api boolean NOT NULL DEFAULT false;
DO
$$
BEGIN
    IF
(
SELECT atttypid
FROM pg_attribute
WHERE attrelid = 'auth.accounts'::regclass
        AND attname = 'can_access_api' AND NOT attisdropped) <> 'boolean'::regtype THEN
        RAISE EXCEPTION 'auth.accounts.can_access_api must be boolean';
END IF;
END $$;
ALTER TABLE auth.accounts
    ALTER COLUMN can_access_api SET DEFAULT false;
UPDATE auth.accounts
SET can_access_api = false
WHERE can_access_api IS NULL;
ALTER TABLE auth.accounts
    ALTER COLUMN can_access_api SET NOT NULL;
