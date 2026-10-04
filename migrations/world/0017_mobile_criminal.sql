-- Until when a mobile is a criminal; null for one that is not.
ALTER TABLE "world"."mobiles" ADD COLUMN IF NOT EXISTS "criminal_until" TIMESTAMP;

COMMENT ON COLUMN "world"."mobiles"."criminal_until" IS 'Until when the mobile is a criminal, in UTC; null for one that is not. Its name is grey until then.';
