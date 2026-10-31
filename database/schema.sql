-- Run against manifesto_explorer. Keep all policy claims in NeedsReview until checked by a human.
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE TABLE IF NOT EXISTS "Parties" (
  "Id" uuid PRIMARY KEY, "Name" varchar(250) NOT NULL UNIQUE, "Abbreviation" varchar(50),
  "Description" text, "WebsiteUrl" text
);
CREATE TABLE IF NOT EXISTS "Manifestos" (
  "Id" uuid PRIMARY KEY, "PartyId" uuid NOT NULL REFERENCES "Parties"("Id") ON DELETE CASCADE,
  "Title" varchar(500) NOT NULL, "ElectionYear" integer NOT NULL, "DocumentUrl" text,
  "SourceName" varchar(300), "StoredFileName" varchar(300), "ContentSha256" varchar(64),
  "PublishedAt" timestamptz, "CreatedAt" timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS "Policies" (
  "Id" uuid PRIMARY KEY, "ManifestoId" uuid NOT NULL REFERENCES "Manifestos"("Id") ON DELETE CASCADE,
  "Title" varchar(500) NOT NULL, "Topic" varchar(150) NOT NULL, "Summary" text NOT NULL,
  "FullText" text NOT NULL, "PageReference" varchar(100), "EvidenceQuote" text,
  "ExtractionMethod" varchar(40) NOT NULL DEFAULT 'Keyword', "Confidence" numeric(4,3),
  "VerificationStatus" varchar(40) NOT NULL DEFAULT 'NeedsReview', "CreatedAt" timestamptz NOT NULL DEFAULT now(),
  "VerifiedAt" timestamptz
);
CREATE INDEX IF NOT EXISTS "IX_Policies_Topic" ON "Policies" ("Topic");
CREATE INDEX IF NOT EXISTS "IX_Policies_Title" ON "Policies" ("Title");
CREATE INDEX IF NOT EXISTS "IX_Policies_VerificationStatus" ON "Policies" ("VerificationStatus");
CREATE INDEX IF NOT EXISTS "IX_Manifestos_PartyId" ON "Manifestos" ("PartyId");
CREATE INDEX IF NOT EXISTS "IX_Manifestos_ContentSha256" ON "Manifestos" ("ContentSha256");
CREATE INDEX IF NOT EXISTS "IX_Policies_ManifestoId" ON "Policies" ("ManifestoId");
CREATE INDEX IF NOT EXISTS "IX_Policies_FullText_Trgm" ON "Policies" USING gin ("FullText" gin_trgm_ops);

-- Initial party directory only. No manifesto claims or policy positions are fabricated.
INSERT INTO "Parties" ("Id", "Name", "Abbreviation", "Description") VALUES
('10000000-0000-0000-0000-000000000001','African National Congress','ANC','Political party in South Africa.'),
('10000000-0000-0000-0000-000000000002','Democratic Alliance','DA','Political party in South Africa.'),
('10000000-0000-0000-0000-000000000003','Economic Freedom Fighters','EFF','Political party in South Africa.'),
('10000000-0000-0000-0000-000000000004','Inkatha Freedom Party','IFP','Political party in South Africa.'),
('10000000-0000-0000-0000-000000000005','ActionSA','ActionSA','Political party in South Africa.'),
('10000000-0000-0000-0000-000000000006','Freedom Front Plus','FF Plus','Political party in South Africa.'),
('10000000-0000-0000-0000-000000000007','uMkhonto weSizwe Party','MK Party','Political party in South Africa.'),
('10000000-0000-0000-0000-000000000008','GOOD','GOOD','Political party in South Africa.'),
('10000000-0000-0000-0000-000000000009','Build One South Africa','BOSA','Political party in South Africa.'),
('10000000-0000-0000-0000-000000000010','African Christian Democratic Party','ACDP','Political party in South Africa.')
ON CONFLICT ("Name") DO NOTHING;
