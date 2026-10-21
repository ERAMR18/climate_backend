CREATE TABLE IF NOT EXISTS "CommunitySchemaVersions" ("Version" integer PRIMARY KEY, "AppliedAt" timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS "Communities" (
  "Id" uuid PRIMARY KEY,
  "Name" varchar(120) NOT NULL,
  "Description" varchar(500),
  "Latitude" numeric(9,6) NOT NULL CHECK ("Latitude" BETWEEN -90 AND 90),
  "Longitude" numeric(9,6) NOT NULL CHECK ("Longitude" BETWEEN -180 AND 180),
  "IsActive" boolean NOT NULL,
  "CreatedAt" timestamptz NOT NULL,
  "Municipality" varchar(120), "Department" varchar(120), "Country" varchar(120)
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Communities_Name" ON "Communities" (upper("Name"));
CREATE INDEX IF NOT EXISTS "IX_Communities_Geography" ON "Communities" ("Department", "Municipality", "IsActive");
CREATE TABLE IF NOT EXISTS "AuditOutbox" (
  "Id" uuid PRIMARY KEY, "Payload" text NOT NULL, "CreatedAt" timestamptz NOT NULL, "PublishedAt" timestamptz
);
CREATE INDEX IF NOT EXISTS "IX_AuditOutbox_PublishedAt_CreatedAt" ON "AuditOutbox" ("PublishedAt", "CreatedAt");
INSERT INTO "CommunitySchemaVersions" ("Version", "AppliedAt") VALUES (1, now()) ON CONFLICT DO NOTHING;
