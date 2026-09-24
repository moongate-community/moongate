CREATE TABLE IF NOT EXISTS auth.accounts
(
    id
    bigint
    PRIMARY
    KEY,
    username
    varchar
(
    255
) NOT NULL,
    hash_password varchar
(
    255
) NOT NULL,
    account_type integer NOT NULL,
    email varchar
(
    255
),
    created_at timestamp NOT NULL,
    last_login_at timestamp,
    updated_at timestamp NOT NULL,
    is_locked boolean NOT NULL
    );

-- Existing development accounts keep their data. Invalid rows or duplicate
-- usernames deliberately stop this migration instead of choosing a winner.
ALTER TABLE auth.accounts
    ALTER COLUMN username SET NOT NULL;
ALTER TABLE auth.accounts
    ALTER COLUMN hash_password SET NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_accounts_username ON auth.accounts(username);
