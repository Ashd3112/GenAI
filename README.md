# AURA AI // RAG-Based Insurance Claims & Policy Assistant

AURA AI is a premium, state-of-the-art intelligent claims examiner and policy analysis assistant built using **ASP.NET Core**, **Microsoft Semantic Kernel**, **Ollama**, and **Microsoft SQL Server**. It enables claims adjusters and operators to upload policy documents and incident reports, run instant semantic vector RAG search, summarize claim histories, and simulate AI-guided coverage decision determinations.

---

## Key Features

1. **Document Library (Vector RAG)**: Upload claims or insurance policies in PDF, Markdown, or text formats. The files are dynamically parsed, split into overlaps, embedded via `nomic-embed-text`, and saved in SQL Server.
2. **Interactive RAG Assistant**: Ask complex insurance coverage questions and retrieve answers grounded in local policies with proper source file citations.
3. **Automated Claim Summarization**: Condense messy claim reports into a clear, clinical structure (Executive Summary, Timeline, Damages, and Next Steps).
4. **Key Information Extraction**: Extract key metadata fields (Claimant, Date of Loss, Claim Amount, Policy Number, Incident Description, and custom outstanding checklist details) in structured format.
5. **AI Coverage Decision Support**: Automatically cross-reference incident damage details against specific policy guidelines and exclusions to generate recommendation outcomes (`Approve`, `Deny`, or `Investigate`) with confidence ratings and reference citations.

---

## Technical Architecture

- **Backend Platform**: ASP.NET Core 10 (Web API & Static File Serving)
- **AI Orchestration**: Microsoft Semantic Kernel
- **Local Models (via Ollama)**:
  - Text Embedding: `nomic-embed-text`
  - Text Chat Completion: `llama3.2`
- **Database Storage**: Local Microsoft SQL Server (`localhost`)
- **Frontend App**: Modern Vanilla HTML5, JavaScript (ES6), and custom Glassmorphic CSS.

---

## Prerequisites

To run this application, make sure you have the following installed on your machine:

1. **.NET SDK**: version 10.0 or higher.
2. **Microsoft SQL Server**: Local instance running at `localhost` with Windows Authentication enabled.
3. **Ollama**: Local service installed.
   - Run `ollama pull llama3.2` to fetch the chat completion model.
   - Run `ollama pull nomic-embed-text` to fetch the embedding model.

---

## Getting Started

### 1. Run the Web API Backend
Open your terminal inside the project root directory and run:

```bash
dotnet run --launch-profile http
```

The application will start, automatically establish connection to your local SQL Server instance, initialize the `InsuranceClaimsAssistant` database, and create the required tables (`Documents` and `VectorRecords`).

Once running, access the web panel at: **[http://localhost:5294](http://localhost:5294)**

### 2. Upload Sample Documents
To test the end-to-end functionality, locate the pre-configured mock files inside the `/samples` folder:
- **Policy**: `samples/auto_policy_2026.txt` (Contains Collision rules, limits, and exclusions for aftermarket custom wheels).
- **Claim**: `samples/auto_claim_john_doe.txt` (Incident report where claimant John Doe hit a utility pole swerving for a deer, estimating $3,200 for bumper damage and $1,300 for aftermarket alloy rims).

**Steps:**
1. Navigate to the **Document Library** tab in the UI.
2. Select **Insurance Policy Guideline**, click the file input, upload `auto_policy_2026.txt`, and click **Process & Index Document**.
3. Switch document type to **Claim Incident / Damage Report**, select `auto_claim_john_doe.txt`, and click upload.
4. Try the **Claim Summarizer** and **Decision Support** tabs to simulate AI-guided claims examination!
