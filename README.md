# South African Manifesto Explorer

A politically neutral voter-information application built with React + Vite, ASP.NET Core Web API (.NET 10), Entity Framework Core, PostgreSQL and PDF text extraction.

## Implemented features

- Search party directory by full party name or abbreviation.
- Search verified policy claims by keyword/phrase, party, topic and election year.
- Party profiles with manifesto documents and their verified policy extracts.
- Side-by-side topic comparison across parties (same topic, source-backed records).
- PDF manifesto ingestion through a protected API endpoint (no admin dashboard).
- Text extraction with page references using PdfPig.
- Hybrid extraction: deterministic topic/keyword matching plus optional AI-assisted policy extraction using an OpenAI-compatible Chat Completions endpoint.
- Every candidate is stored as `NeedsReview`; only explicitly verified policies appear in public search and comparison.
- Exact evidence quote, page number, extraction method, confidence, source URL and source PDF are retained.
- Duplicate PDF detection using SHA-256.
- Protected API endpoints to inspect pending candidates and verify/reject them.
- Initial directory of South African parties in `database/schema.sql`. No party policy claims are fabricated or preloaded.

## Requirements

- .NET 10 SDK
- Node.js 20+
- Docker Desktop (or PostgreSQL 17)

## 1. Start PostgreSQL

From the project root:

```bash
docker compose up -d
```

The local development database is `manifesto_explorer` with username/password `postgres`/`postgres`. Change these credentials outside local development.

## 2. Configure and create the database

Set the connection string in `backend/ManifestoExplorer.Api/appsettings.json` or override it with `ConnectionStrings__DefaultConnection`.

Recommended: create an EF Core migration so the schema remains versioned with the application:

```bash
cd backend/ManifestoExplorer.Api
dotnet tool install --global dotnet-ef
dotnet restore
dotnet ef migrations add InitialCreate
dotnet ef database update
```

The EF migration seeds an initial party directory (names only; no manifesto claims). The equivalent SQL schema and initial party directory are also in `database/schema.sql`. If using that script, execute it against `manifesto_explorer` in pgAdmin/psql instead of also running a conflicting hand-maintained schema. When changing models later, create a new EF migration and update the SQL reference script too.

## 3. Configure ingestion and AI extraction

Set these environment variables on the backend process (do not put secrets in React's `VITE_` variables):

```bash
# Protects manifesto upload, pending-candidate review and verification endpoints.
# Use a long random secret. Example only; generate your own.
Ingestion__ApiKey=replace-with-a-long-random-secret

# Optional OpenAI-compatible Chat Completions API.
# If omitted, deterministic keyword extraction still works.
AiExtraction__Endpoint=https://api.openai.com/v1/chat/completions
AiExtraction__ApiKey=replace-with-provider-api-key
AiExtraction__Model=gpt-4o-mini
AiExtraction__MaxPages=80
```

The AI endpoint must support bearer authentication and Chat Completions JSON responses. An OpenAI-compatible provider can be used. Keep the endpoint and API key server-side.

The hybrid process works as follows:

1. PdfPig extracts selectable text page by page.
2. Keyword rules identify passages relevant to immigration, social grants, policing, education, healthcare, jobs, housing, corruption, economy, energy and transport.
3. If AI is configured, the model identifies explicit policy statements and returns a concise neutral summary plus an exact source quote.
4. The backend checks that the AI quote is actually present in the extracted page text before accepting it.
5. Candidates are saved with `NeedsReview`, never automatically published.
6. A human checks the source and explicitly verifies or rejects each candidate.

AI output is a drafting aid, not authoritative interpretation. The human reviewer remains responsible for the accuracy, neutrality and context of the public policy record.

## 4. Run the backend

```bash
cd backend/ManifestoExplorer.Api
dotnet restore
dotnet run --urls http://localhost:5000
```

Swagger is available at `http://localhost:5000/swagger`; health check is `http://localhost:5000/api/health`.

## 5. Run React

In another terminal:

```bash
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173`. If the API is hosted elsewhere, create `frontend/.env.local`:

```env
VITE_API_URL=http://localhost:5000/api
```

## Ingest a manifesto (API workflow, no admin dashboard)

First retrieve the party ID:

```bash
curl http://localhost:5000/api/parties
```

Upload a text-based PDF (25 MB maximum). Example using bash:

```bash
curl -X POST http://localhost:5000/api/manifestos/upload \
  -H "X-Ingestion-Key: $Ingestion__ApiKey" \
  -F "file=@./party-manifesto.pdf" \
  -F "partyId=PASTE-PARTY-GUID-HERE" \
  -F "title=2024 Election Manifesto" \
  -F "electionYear=2024" \
  -F "sourceUrl=https://party.example/manifesto.pdf"
```

The upload response includes candidate policy IDs. Uploaded PDFs are stored under `backend/ManifestoExplorer.Api/App_Data/manifestos` by default; back up this directory and use persistent storage in deployment. The original PDF can be viewed at `/api/manifestos/{manifestoId}/document`.

Image-only/scanned PDFs are not supported by this implementation because OCR is not configured. The API reports this clearly rather than pretending it extracted text.

## Review and verify extracted candidates

List pending candidates:

```bash
curl http://localhost:5000/api/manifestos/ingestion/pending \
  -H "X-Ingestion-Key: $Ingestion__ApiKey"
```

First edit the candidate so the title, topic and summary are neutral and accurate. The exact evidence quote must remain inside the extracted passage:

```bash
curl -X PATCH http://localhost:5000/api/manifestos/policies/POLICY-GUID-HERE/review \
  -H "X-Ingestion-Key: $Ingestion__ApiKey" \
  -H "Content-Type: application/json" \
  -d '{"title":"Social grant policy","topic":"Social grants","summary":"A concise neutral summary based on the cited commitment.","evidenceQuote":"EXACT QUOTE COPIED FROM THE SOURCE PASSAGE"}'
```

After checking the quoted passage and page in the original manifesto, verify a candidate:

```bash
curl -X PATCH http://localhost:5000/api/manifestos/policies/POLICY-GUID-HERE/verification \
  -H "X-Ingestion-Key: $Ingestion__ApiKey" \
  -H "Content-Type: application/json" \
  -d '{"status":"Verified"}'
```

Use `NeedsReview` or `Rejected` to change its status. Verification requires an evidence quote present in the extracted source text. Protect this key and these endpoints; the project intentionally has no admin dashboard or user login yet. For production, place ingestion behind VPN/identity-aware access, rotate the key, add audit logging, and use proper admin authentication.

## Public API

- `GET /api/health`
- `GET /api/parties`
- `GET /api/parties/{id}`
- `GET /api/manifestos`
- `GET /api/manifestos/{id}/document`
- `GET /api/policies/search?keyword=grants&party=ANC&topic=Social%20grants&year=2024`
- `GET /api/policies/compare?topic=Policing&year=2024`

Protected ingestion API:

- `POST /api/manifestos/upload` (multipart form: `file`, `partyId`, `title`, `electionYear`, optional `sourceUrl`)
- `GET /api/manifestos/ingestion/pending`
- `PATCH /api/manifestos/policies/{policyId}/review` with title, topic, summary and exact evidence quote
- `PATCH /api/manifestos/policies/{policyId}/verification` with `{ "status": "Verified" | "NeedsReview" | "Rejected" }`

## Important limitations and production checklist

- This repository does not include licensed manifesto PDFs or claim that every party's manifesto is already indexed. Populate it with authentic publications and cite official sources.
- Only human-verified records are publicly searchable/compared. Search can therefore return no results until data is ingested and reviewed.
- OCR for scanned PDFs, multi-language translation, automated official-source discovery, user accounts, audit history and an admin dashboard are not included.
- Add authentication/authorization, rate limits, upload malware scanning, storage quotas, durable object storage, backups, audit trails, HTTPS, monitoring and secret management before public deployment.
- Review copyright and terms before storing or displaying manifesto text. Prefer short evidence quotations and link to the source document.
- Keep election year and publication date visible; policies may change between elections.
