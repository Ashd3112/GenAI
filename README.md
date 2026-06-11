# AURA AI // RAG-Based Insurance Claims & Policy Assistant

AURA AI is a premium, state-of-the-art intelligent claims examiner and policy analysis assistant built using **ASP.NET Core 10**, **Microsoft Semantic Kernel**, **Ollama**, and **Microsoft SQL Server**. It enables claims adjusters and operators to upload policy documents and incident reports, run instant semantic vector RAG search, summarize claim histories, simulate AI-guided coverage decision determinations, and monitor application health with full audit logging.

---

## Key Features

1. **Document Library (Vector RAG)**: Upload claims or insurance policies in PDF, Markdown, text, or JSON formats. Files are dynamically parsed, chunked, embedded via `nomic-embed-text`, and indexed in SQL Server.
2. **Interactive RAG Assistant**: Ask complex insurance coverage questions and retrieve answers grounded in local policies with proper source file citations.
3. **Automated Claim Summarization**: Condense messy claim reports into a clear, clinical structure (Executive Summary, Timeline, Damages, and Next Steps).
4. **Key Information Extraction**: Extract key metadata fields (Claimant, Date of Loss, Claim Amount, Policy Number, Incident Description, and custom checklist details) in a structured JSON format.
5. **AI Coverage Decision Support**: Automatically cross-reference incident damage details against specific policy guidelines and exclusions to generate recommendation outcomes (`Approve`, `Deny`, or `Investigate`) with confidence ratings and reference citations.
6. **Centralized Logging & Auditing**: Complete request-response auditing via custom middleware powered by Serilog with console and daily rolling file outputs.
7. **Robust Error Handling**: Centralized global exception handler mapping errors to RFC 7807 `ProblemDetails` JSON responses with correlation tracking.
8. **Diagnostic Health Checks**: Deep check endpoints for verifying local SQL Server database availability and Ollama API readiness.

---

## Technical Architecture

- **Backend Platform**: ASP.NET Core 10 (Web API & SPA Static File Server)
- **AI Orchestration**: Microsoft Semantic Kernel
- **Local Models (via Ollama)**:
  - Text Embedding: `nomic-embed-text`
  - Text Chat Completion: `llama3.2`
- **Database Storage**: Local Microsoft SQL Server (database: `InsuranceClaimsAssistant`)
- **Logging Infrastructure**: Serilog with Console and File Sinks (logs written to `App_Data/logs/`)
- **Frontend App**: Modern Vanilla HTML5, JavaScript (ES6), and custom Glassmorphic CSS.

---

## Prerequisites

To run this application, make sure you have the following installed on your machine:

1. **.NET SDK**: version 10.0 or higher.
2. **Microsoft SQL Server**: Local instance running at `localhost` with Windows Authentication enabled.
3. **Ollama**: Local service installed and running.
   - Run `ollama pull llama3.2` to fetch the chat completion model.
   - Run `ollama pull nomic-embed-text` to fetch the embedding model.

---

## Getting Started

### 1. Run the Web API Backend
Open your terminal inside the project root directory and run:

```bash
dotnet run --launch-profile http
```

The application will start, automatically establish a connection to your local SQL Server instance, initialize the `InsuranceClaimsAssistant` database, and create the required tables (`Documents` and `VectorRecords`).

Once running, access the web panel at: **[http://localhost:5294](http://localhost:5294)**

### 2. Verify Health Checks
To ensure both the SQL Server database and Ollama are reachable and healthy, visit:
**[http://localhost:5294/health](http://localhost:5294/health)**

This endpoint returns detailed diagnostic JSON, such as:
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "Database",
      "status": "Healthy",
      "description": "SQL Server database connection is healthy."
    },
    {
      "name": "Ollama",
      "status": "Healthy",
      "description": "Ollama service is running and healthy."
    }
  ],
  "totalDuration": "00:00:00.0450000"
}
```

### 3. Upload Sample Documents
To test the end-to-end functionality, locate the pre-configured mock files inside the `/samples` folder:
- **Policy**: `samples/auto_policy_2026.txt` (Contains Collision rules, limits, and exclusions for aftermarket custom wheels).
- **Claim**: `samples/auto_claim_john_doe.txt` (Incident report where claimant John Doe hit a utility pole swerving for a deer, estimating $3,200 for bumper damage and $1,300 for aftermarket alloy rims).

**Steps:**
1. Navigate to the **Document Library** tab in the UI.
2. Select **Insurance Policy Guideline**, click the file input, upload `auto_policy_2026.txt`, and click **Process & Index Document**.
3. Switch document type to **Claim Incident / Damage Report**, select `auto_claim_john_doe.txt`, and click upload.
4. Try the **Claim Summarizer** and **Decision Support** tabs to simulate AI-guided claims examination!

---

## API Endpoints

### Documents & Indexing
* **`POST /api/documents/upload`**  
  Uploads and indexes a policy or claim document (`multipart/form-data` with `file` and `type` fields).
* **`GET /api/documents`**  
  Lists all processed documents, categorized by type, along with their chunk counts.
* **`DELETE /api/documents/{fileName}`**  
  Deletes a document from the local store and SQL Server index.

### RAG & AI Orchestration
* **`POST /api/assistant/query`**  
  Performs semantic search retrieval and prompts the assistant using context-grounded rules.
* **`POST /api/assistant/summarize`**  
  Generates a structured medical/incident report summary for the selected claim document.
* **`POST /api/assistant/extract`**  
  Extracts structured key information fields (Claimant, Date of Loss, Policy Details) from a claim.
* **`POST /api/assistant/decision`**  
  Cross-checks damages in the claim against coverage exclusions in the policy to recommend an outcome (`Approve`, `Deny`, `Investigate`).

### Diagnostics
* **`GET /health`**  
  Returns comprehensive system health status for Database and Ollama integrations.

---

## Diagnostics & Infrastructure

### 1. Centralized Logging (Serilog)
All logs are enriched with request-specific properties and routed concurrently to:
- **Console**: Structured output template indicating timestamp, log level, and message.
- **Rolling Log Files**: Located in `App_Data/logs/insurance-assistant-yyyyMMdd.log` rolling daily.

### 2. Audit Middleware (`RequestResponseLoggingMiddleware`)
Audits all incoming requests and outgoing responses, automatically calculating processing duration in milliseconds and appending a correlation `TraceId` to all downstream logs within the execution context.

### 3. Centralized Exception Handling (`GlobalExceptionHandler`)
Captures all unhandled exceptions within the HTTP pipeline, logs the trace and error information, and returns structured error payloads adhering to the **RFC 7807 Problem Details** standard to prevent exposing sensitive internal stack details.

