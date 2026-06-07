# Top 10 Production-Ready Code Scenarios

---

## DocPipeline — AI-Powered Document Processing (Implemented)

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

---

## 1. 🤖 AI-Powered Document Processing Pipeline

Scenario: Build a system that ingests financial documents (PDFs, invoices), extracts structured data using Azure OpenAI, and stores results in Cosmos DB.

Why relevant: Direct match to your Deloitte audit background + AI skills

Tech: Azure OpenAI, Document Intelligence, Cosmos DB, Azure Functions

---

## 2. 🔄 Event-Driven Microservices with Saga Pattern

Scenario: Build an order processing system using microservices that handles distributed transactions using the Saga pattern with compensating transactions.

Why relevant: Tests architecture thinking at EM level

Tech: .NET Core, Azure Service Bus, SQL Server, Docker

---

## 3. 📊 Real-Time Financial Data Streaming

Scenario: Build a real-time pipeline that ingests high-volume transaction data, detects anomalies using ML, and triggers alerts.

Why relevant: Mirrors your Deloitte financial data processing work

Tech: Azure Event Hubs, Stream Analytics, Azure ML, Power BI

---

## 4. 🔐 Zero Trust API Gateway

Scenario: Design and build a secure API gateway with OAuth2, rate limiting, RBAC, and threat detection — production-hardened.

Why relevant: Security + API design is core EM responsibility

Tech: Azure API Management, .NET Core, Key Vault, AAD

---

## 5. 🧠 RAG-Based Internal Knowledge Bot

Scenario: Build a chatbot that answers questions from internal documents using Retrieval-Augmented Generation (RAG).

Why relevant: Extremely hot right now — every enterprise is building this

Tech: Azure OpenAI, Azure AI Search, .NET Core, Blob Storage

---

## 6. 🚀 CI/CD Pipeline with Quality Gates

Scenario: Build a full DevSecOps pipeline with automated testing, security scanning, performance benchmarks, and staged rollouts (blue/green deployment).

Why relevant: Direct match to your DevOps certifications and release governance experience

Tech: Azure DevOps, Docker, Kubernetes (AKS), SonarQube

---

## 7. 📦 Multi-Tenant SaaS Platform

Scenario: Build a multi-tenant backend where each tenant has isolated data, custom configurations, and usage-based billing.

Why relevant: Tests scalable architecture thinking — classic EM-level problem

Tech: .NET Core, Azure SQL (row-level security), Azure API Management

---

## 8. 🔍 Observability & Self-Healing System

Scenario: Build a microservices app with full observability — distributed tracing, structured logging, metrics dashboards — and auto-remediation on failure.

Why relevant: Matches your 25% incident reduction story — great talking point

Tech: Azure Monitor, Application Insights, .NET Core, Logic Apps

---

## 9. 🤝 AI Agent for Workflow Automation

Scenario: Build an AI agent that monitors a queue, makes decisions using LLM reasoning, and autonomously executes multi-step business workflows.

Why relevant: Directly mirrors your AI automation POC at Deloitte

Tech: Azure OpenAI (function calling), Semantic Kernel, Service Bus, .NET Core

---

## 10. 🌐 Resilient Distributed Caching Layer

Scenario: Build a high-availability caching system with cache-aside pattern, cache invalidation strategies, and fallback mechanisms under load.

Why relevant: Tests production-readiness thinking — performance under pressure

Tech: Azure Cache for Redis, .NET Core, Circuit Breaker (Polly)
