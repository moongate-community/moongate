-- Claims use the letter serial; the column-owned sequence satisfies the persistence schema convention and is never used for claims.
CREATE SEQUENCE world.book_attachment_claims_letter_id_seq
    AS bigint MINVALUE 1 MAXVALUE 4294967295 START WITH 1 NO CYCLE;

CREATE TABLE world.book_attachment_claims (
    letter_id bigint NOT NULL,
    claimant_id bigint NOT NULL,
    claimed_at bigint NOT NULL,
    CONSTRAINT world_book_attachment_claims_pkey PRIMARY KEY (letter_id)
);
ALTER SEQUENCE world.book_attachment_claims_letter_id_seq OWNED BY world.book_attachment_claims.letter_id;
COMMENT ON TABLE world.book_attachment_claims IS 'A durable receipt preventing a physical letter from delivering its attachments twice.';
COMMENT ON COLUMN world.book_attachment_claims.letter_id IS 'The letter''s existing item serial; no new identity is allocated.';
COMMENT ON COLUMN world.book_attachment_claims.claimant_id IS 'The character who withdrew the attachments.';
COMMENT ON COLUMN world.book_attachment_claims.claimed_at IS 'The withdrawal time in UTC Unix milliseconds.';

-- Lock the referenced letter until receipt insertion finishes. A concurrent deletion must then see and remove the receipt.
CREATE FUNCTION world.validate_book_attachment_claim_letter() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    PERFORM id FROM world.items WHERE id = NEW.letter_id FOR KEY SHARE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'The attachment receipt references a missing letter.'
            USING ERRCODE = '23503', CONSTRAINT = 'fk_book_attachment_claims_letter';
    END IF;
    RETURN NEW;
END;
$$;

CREATE CONSTRAINT TRIGGER book_attachment_claim_letter_exists
    AFTER INSERT OR UPDATE ON world.book_attachment_claims
    DEFERRABLE INITIALLY IMMEDIATE
    FOR EACH ROW EXECUTE FUNCTION world.validate_book_attachment_claim_letter();

-- A world save can delete an old container, cascade through a moved letter, and restore the live letter later in the
-- same transaction. Check its final existence before cleaning the receipt so that this never replenishes its rewards.
CREATE FUNCTION world.cleanup_deleted_book_attachment_claim() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM world.items WHERE id = OLD.id) THEN
        DELETE FROM world.book_attachment_claims WHERE letter_id = OLD.id;
    END IF;
    RETURN NULL;
END;
$$;

CREATE CONSTRAINT TRIGGER book_attachment_claim_deleted_letter
    AFTER DELETE ON world.items
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW EXECUTE FUNCTION world.cleanup_deleted_book_attachment_claim();
