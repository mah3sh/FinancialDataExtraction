# DocPipeline — Architecture & Interview Guide

## 1. High-Level Context Diagram

```mermaid
graph TB
    User([👤 User / Browser])
    FE[React SPA\nAzure Static Web Apps]
    API[ASP.NET Core 8 API\nAzure Container Apps]
    DB[(Azure SQL\nDocPipeline DB)]
    KV[Azure Key Vault\nSecrets]
    AI[Azure OpenAI\nGPT-4o]
    BLOB[Azure Blob Storage\nDocument Files]
    MON[Azure Monitor\nApp Insights]

    User -->|HTTPS| FE
    FE -->|REST + JWT\nCORS gated| API
    API -->|EF Core\nTLS| DB
    API -->|Key Vault ref| KV
    API -->|Chat completions\nGPT-4o| AI
    API -->|Upload / Read| BLOB
    API -->|Logs + Metrics| MON
    KV -.->|Injects secrets at runtime| API
```

---

## 2. Service / Container Diagram with Data Flows

```mermaid
sequenceDiagram
    participant Browser
    participant Frontend as React SPA<br/>(Static Web Apps)
    participant API as API Container<br/>(Container Apps)
    participant Worker as Background Worker<br/>(IHostedService in API)
    participant Queue as Channel&lt;Guid&gt;<br/>(in-process)
    participant SQL as Azure SQL
    participant Blob as Azure Blob
    participant OpenAI as Azure OpenAI

    Note over Browser,Frontend: CORS boundary enforced at API
    Browser->>Frontend: POST /api/auth/login
    Frontend->>API: POST /api/auth/login (JSON)
    API->>SQL: Validate identity hash
    SQL-->>API: IdentityUser
    API-->>Frontend: JWT (HS256, 60 min)
    Frontend-->>Browser: Store JWT in localStorage

    Browser->>Frontend: Upload invoice.pdf
    Frontend->>API: POST /api/documents (multipart, Bearer)
    API->>API: Validate content-type + size
    API->>Blob: Save file → storagePath
    API->>SQL: INSERT Document (Pending)
    API->>Queue: Enqueue(documentId)
    API-->>Frontend: 202 Accepted {id, status: Pending}

    loop Poll every 3s
        Frontend->>API: GET /api/documents/{id}/status
        API->>SQL: SELECT Document WHERE Id=?
        API-->>Frontend: {status: Processing|Completed|Failed}
    end

    Worker->>Queue: DequeueAsync()
    Queue-->>Worker: documentId
    Worker->>SQL: UPDATE status=Processing (RowVersion check)
    Worker->>Blob: Read file bytes
    Worker->>OpenAI: Chat completion (vision/text)
    OpenAI-->>Worker: Dynamic JSON
    Worker->>SQL: UPDATE status=Completed, ExtractedDataJson=?

    Frontend->>API: GET /api/documents/{id}/result
    API->>SQL: SELECT Document
    API-->>Frontend: {extractedData: {...dynamic...}}
```

---

## 3. Environment Variables by Component

### API (Azure Container Apps / App Settings)

| Variable | Source | Example |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | App Settings | `Server=...;Database=DocPipeline;...` |
| `Jwt__Issuer` | App Settings | `docpipeline` |
| `Jwt__Audience` | App Settings | `docpipeline` |
| `Jwt__ExpiresMinutes` | App Settings | `60` |
| `AzureOpenAI__DeploymentName` | App Settings | `gpt-4o` |
| `Cors__AllowedOrigins__0` | App Settings | `https://myapp.azurestaticapps.net` |
| `UseMockAI` | App Settings | `false` |
| `Jwt__Key` | **Key Vault reference** | `@Microsoft.KeyVault(SecretUri=https://...)` |
| `AzureOpenAI__Endpoint` | **Key Vault reference** | `@Microsoft.KeyVault(...)` |
| `AzureOpenAI__ApiKey` | **Key Vault reference** | `@Microsoft.KeyVault(...)` |

### Frontend (Static Web Apps / Build Args)

| Variable | Set at | Example |
|---|---|---|
| `VITE_API_BASE_URL` | Build time / SWA config | `https://api.myapp.io` |

---

## 4. Migration Strategy

**Chosen: Startup migration (`db.Database.MigrateAsync()` in `Program.cs`)**

```
app startup → MigrateAsync() → DB schema up-to-date → app serves traffic
```

**Trade-off table:**

| Strategy | Pros | Cons |
|---|---|---|
| **Startup (chosen)** | Simple, zero extra tooling, always in sync | Locks startup for seconds; multi-replica race on first deploy |
| Pipeline/CLI migration | No startup delay; safe for big tables | Requires separate deploy step; CI/CD complexity |

**Why startup for MVP:** Single replica in Container Apps; assessments value simplicity. Production with scale-out → switch to pipeline migration + advisory lock or EF Bundles.

---

## 5. Production CORS

```json
// appsettings.Production.json
{
  "Cors": {
    "AllowedOrigins": [
      "https://myapp.azurestaticapps.net",
      "https://www.mycompany.com"
    ]
  }
}
