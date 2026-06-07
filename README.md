## DocPipeline — AI-Powered Document Processing

> Full source: [`DocPipeline/`](DocPipeline/)

### Prerequisites

| Tool | Version | Install |
|---|---|---|
| Docker + Docker Compose | v24+ | [docker.com](https://docker.com) |
| .NET SDK | 8.0 | [dot.net](https://dot.net) (backend only) |
| Node.js | 20+ | [nodejs.org](https://nodejs.org) (frontend only) |

---

### Option A — Full stack with Docker (recommended)

```bash
# 1. Copy env file
cp DocPipeline/.env.example DocPipeline/.env

# 2. Start everything (SQL Server + API + Frontend)
cd DocPipeline
docker-compose up --build
```

| Service | URL |
|---|---|
| Frontend | http://localhost:3000 |
| Swagger UI | http://localhost:8080/swagger |
| Health check | http://localhost:8080/health |

> `USE_MOCK_AI=true` is set by default in `.env.example` — no Azure credentials needed to demo.

---

### Option B — Backend only (.NET SDK)

```bash
cd DocPipeline/backend

# Restore and build
dotnet restore && dotnet build

# Set local secrets (never commit these)
dotnet user-secrets set "Jwt:Key" "dev-secret-minimum-32-chars-here!!" \
  --project src/DocPipeline.API
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<resource>.openai.azure.com/" \
  --project src/DocPipeline.API
dotnet user-secrets set "AzureOpenAI:ApiKey" "<your-key>" \
  --project src/DocPipeline.API

# Run API (mock AI is on by default in Development)
dotnet run --project src/DocPipeline.API

# Swagger: http://localhost:5000/swagger
```

---

### Option C — Frontend only (Node.js)

```bash
cd DocPipeline/frontend
npm install

# Point at your running API
echo "VITE_API_BASE_URL=http://localhost:5000" > .env.local

npm run dev
# Opens http://localhost:5173
```

---

### Run tests

```bash
cd DocPipeline/backend
dotnet test tests/DocPipeline.Tests
```

Covers:
- 8 domain/service unit tests (Document state machine, validation, auth)
- 3 integration tests (register → login → upload → poll → result)

---

### Demo flow

1. Open the frontend (http://localhost:3000)
2. Register an **Uploader** account
3. Upload any PDF or image (PNG, JPEG, WebP — max 20 MB)
4. Watch the status badge: `Pending → Processing → Completed`
5. Expand the document row to view AI-extracted JSON
6. Register a second account as **Reviewer** to see all documents

---

### Connect real Azure OpenAI

Edit `DocPipeline/.env` (or set App Settings in Azure):

```env
USE_MOCK_AI=false
AZURE_OPENAI_ENDPOINT=https://<your-resource>.openai.azure.com/
AZURE_OPENAI_API_KEY=<your-api-key>
AZURE_OPENAI_DEPLOYMENT=gpt-4o
```

---

### Key source files

| File | What it does |
|---|---|
| [DocPipeline/backend/src/DocPipeline.Domain/Entities/Document.cs](DocPipeline/backend/src/DocPipeline.Domain/Entities/Document.cs) | Domain entity + status transition guard clauses |
| [DocPipeline/backend/src/DocPipeline.Application/Services/DocumentService.cs](DocPipeline/backend/src/DocPipeline.Application/Services/DocumentService.cs) | Business logic, validation, role-based access |
| [DocPipeline/backend/src/DocPipeline.Infrastructure/Services/AzureOpenAiExtractionService.cs](DocPipeline/backend/src/DocPipeline.Infrastructure/Services/AzureOpenAiExtractionService.cs) | GPT-4o extraction (real Azure) |
| [DocPipeline/backend/src/DocPipeline.Infrastructure/Services/MockExtractionService.cs](DocPipeline/backend/src/DocPipeline.Infrastructure/Services/MockExtractionService.cs) | Mock extraction for local dev |
| [DocPipeline/backend/src/DocPipeline.Infrastructure/Processing/DocumentProcessingWorker.cs](DocPipeline/backend/src/DocPipeline.Infrastructure/Processing/DocumentProcessingWorker.cs) | Background IHostedService + concurrency handling |
| [DocPipeline/backend/src/DocPipeline.API/Program.cs](DocPipeline/backend/src/DocPipeline.API/Program.cs) | DI wiring, JWT, CORS, Swagger, startup migration |
| [DocPipeline/frontend/src/App.tsx](DocPipeline/frontend/src/App.tsx) | Root React component |
| [DocPipeline/ARCHITECTURE.md](DocPipeline/ARCHITECTURE.md) | Mermaid diagrams + interview talking points |

