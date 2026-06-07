# DocPipeline — Quick Start

## Run locally in 3 commands

```bash
# 1. Copy and edit env vars
cp .env.example .env
# Edit .env: set USE_MOCK_AI=true (no Azure creds needed)

# 2. Start everything
docker-compose up --build

# 3. Open
# Frontend: http://localhost:3000
# Swagger:  http://localhost:8080/swagger
# Health:   http://localhost:8080/health
```

## Run backend only (needs .NET 8 SDK + SQL Server)

```bash
cd backend

# Restore + build
dotnet restore && dotnet build

# Set secrets (never commit these)
dotnet user-secrets set "Jwt:Key" "dev-secret-minimum-32-chars-here!!" --project src/DocPipeline.API
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://..." --project src/DocPipeline.API
dotnet user-secrets set "AzureOpenAI:ApiKey" "sk-..." --project src/DocPipeline.API

# Run (UseMockAI=true in appsettings.Development.json — no Azure creds needed)
dotnet run --project src/DocPipeline.API

# Run tests
dotnet test tests/DocPipeline.Tests
```

## Run frontend only

```bash
cd frontend
npm install
# Create .env.local
echo "VITE_API_BASE_URL=http://localhost:5000" > .env.local
npm run dev
# Opens http://localhost:5173
```

## Demo flow (with mock AI)

1. Open http://localhost:3000
2. Register with role **Uploader**
3. Upload any PDF or image file
4. Watch status change Pending → Processing → Completed
5. Expand the row to see AI-extracted JSON
6. Register a second account as **Reviewer** — sees all documents

## Test the deployed app on Azure

### Get your deployed URLs

```bash
az deployment group show \
  --resource-group docpipeline-prod-rg \
  --name main \
  --query properties.outputs \
  --output json
```

The output contains `apiUrl` and `frontendUrl`. Substitute them below.

### 1. Health check

```bash
curl https://<apiUrl>/health
# expect: {"status":"Healthy"}
```

### 2. Swagger UI

Open in browser:
```
https://<apiUrl>/swagger
```

### 3. API flow via curl

**Register a user:**
```bash
curl -X POST https://<apiUrl>/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test@12345","role":"Uploader"}'
```

**Login and capture the token:**
```bash
TOKEN=$(curl -s -X POST https://<apiUrl>/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test@12345"}' \
  | jq -r '.token')
```

**Upload a document (PDF/image, max 20 MB):**
```bash
curl -X POST https://<apiUrl>/api/documents \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@/path/to/sample.pdf"
# returns a document ID and status "Pending"
```

**Poll status until "Completed":**
```bash
curl https://<apiUrl>/api/documents/<id>/status \
  -H "Authorization: Bearer $TOKEN"
```

**Get extraction result:**
```bash
curl https://<apiUrl>/api/documents/<id>/result \
  -H "Authorization: Bearer $TOKEN"
```

### 4. Frontend UI

Open `https://<frontendUrl>` in the browser — register, log in, upload a document, and watch the extraction results appear.

### 5. Check logs if anything fails

```bash
az containerapp logs show \
  --name docpipeline-prod-api \
  --resource-group docpipeline-prod-rg \
  --follow
```

---

## Key files

| File | Purpose |
|---|---|
| [backend/src/DocPipeline.Domain/Entities/Document.cs](backend/src/DocPipeline.Domain/Entities/Document.cs) | Domain entity + status transitions |
| [backend/src/DocPipeline.Application/Services/DocumentService.cs](backend/src/DocPipeline.Application/Services/DocumentService.cs) | Business logic, validation, auth |
| [backend/src/DocPipeline.Infrastructure/Services/AzureOpenAiExtractionService.cs](backend/src/DocPipeline.Infrastructure/Services/AzureOpenAiExtractionService.cs) | GPT-4o extraction (real) |
| [backend/src/DocPipeline.Infrastructure/Services/MockExtractionService.cs](backend/src/DocPipeline.Infrastructure/Services/MockExtractionService.cs) | Mock for local dev |
| [backend/src/DocPipeline.Infrastructure/Processing/DocumentProcessingWorker.cs](backend/src/DocPipeline.Infrastructure/Processing/DocumentProcessingWorker.cs) | Background IHostedService |
| [backend/src/DocPipeline.API/Program.cs](backend/src/DocPipeline.API/Program.cs) | DI wiring, middleware, JWT |
| [frontend/src/App.tsx](frontend/src/App.tsx) | Root React component |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Mermaid diagrams + interview guide |
