-- Claims use the letter serial; the column-owned sequence satisfies the persistence schema convention and is never used for claims.
CREATE SEQUENCE world.book_attachment_claims_letter_id_seq
    AS bigint MINVALUE 1 MAXVALUE 4294967295 START WITH 1 NO CYCLE;

CREATE TABLE world.book_attachment_claims (
    letter_id bigint NOT NULL,
    claimant_id bigint NOT NULL,
    claimed_at bigint NOT NULL,
    CONSTRAINT world_book_attachment_claims_pkey PRIMARY KEY (letter_id),
    CONSTRAINT fk_book_attachment_claims_letter FOREIGN KEY (letter_id) REFERENCES world.items(id) ON DELETE CASCADE
);
ALTER SEQUENCE world.book_attachment_claims_letter_id_seq OWNED BY world.book_attachment_claims.letter_id;
COMMENT ON TABLE world.book_attachment_claims IS 'A durable receipt preventing a physical letter from delivering its attachments twice.';
COMMENT ON COLUMN world.book_attachment_claims.letter_id IS 'The letter''s existing item serial; no new identity is allocated.';
COMMENT ON COLUMN world.book_attachment_claims.claimant_id IS 'The character who withdrew the attachments.';
COMMENT ON COLUMN world.book_attachment_claims.claimed_at IS 'The withdrawal time in UTC Unix milliseconds.';
