using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UglyToad.PdfPig;
using InsuranceAssistant;

var builder = WebApplication.CreateBuilder(args);

// Add Services
builder.Services.AddSingleton<HttpClient>();
builder.Services.AddSingleton<VectorStore>();
builder.Services.AddSingleton<Orchestrator>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();
app.UseStaticFiles();

// Directories
var docsDir = Path.Combine(app.Environment.ContentRootPath, "App_Data", "documents");
if (!Directory.Exists(docsDir))
{
    Directory.CreateDirectory(docsDir);
}

// 1. Upload Document (Policy or Claim)
app.MapPost("/api/documents/upload", async (HttpRequest request, VectorStore vectorStore) =>
{
    var file = request.Form.Files.GetFile("file");
    var type = request.Form["type"].ToString();

    if (file == null || file.Length == 0)
    {
        return Results.BadRequest("No file uploaded.");
    }

    if (type != "Policy" && type != "Claim")
    {
        return Results.BadRequest("Type must be either 'Policy' or 'Claim'.");
    }

    var filePath = Path.Combine(docsDir, file.FileName);
    
    // Save file locally
    using (var stream = new FileStream(filePath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    // Extract text
    string text = string.Empty;
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

    try
    {
        if (ext == ".pdf")
        {
            using (var pdf = PdfDocument.Open(filePath))
            {
                var sb = new StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    sb.AppendLine(page.Text);
                }
                text = sb.ToString();
            }
        }
        else if (ext == ".txt" || ext == ".md" || ext == ".json")
        {
            text = await File.ReadAllTextAsync(filePath);
        }
        else
        {
            return Results.BadRequest("Unsupported file format. Only PDF, TXT, MD, and JSON are supported.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return Results.BadRequest("The document contains no extractable text.");
        }

        // Index in VectorStore
        await vectorStore.IndexDocumentAsync(file.FileName, text, type);

        return Results.Ok(new { message = $"Successfully uploaded and indexed {file.FileName} ({type})." });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to process document: {ex.Message}");
    }
}).DisableAntiforgery();

// 2. List Documents
app.MapGet("/api/documents", (VectorStore vectorStore) =>
{
    var records = vectorStore.ListDocuments();
    var docs = records
        .GroupBy(r => new { r.SourceFile, r.Type })
        .Select(g => new
        {
            FileName = g.Key.SourceFile,
            Type = g.Key.Type,
            Chunks = g.Count()
        })
        .ToList();

    return Results.Ok(docs);
});

// 3. Delete Document
app.MapDelete("/api/documents/{fileName}", (string fileName, VectorStore vectorStore) =>
{
    var filePath = Path.Combine(docsDir, fileName);
    if (File.Exists(filePath))
    {
        File.Delete(filePath);
    }
    
    vectorStore.DeleteDocument(fileName);
    return Results.Ok(new { message = $"Document {fileName} deleted successfully." });
});

// 4. RAG Chat / Query
app.MapPost("/api/assistant/query", async (QueryRequest request, Orchestrator orchestrator) =>
{
    if (string.IsNullOrWhiteSpace(request.Prompt))
    {
        return Results.BadRequest("Prompt cannot be empty.");
    }

    var answer = await orchestrator.AskQueryAsync(request.Prompt, request.Type);
    return Results.Ok(new { response = answer });
});

// 5. Summarize Claim
app.MapPost("/api/assistant/summarize", async (FileRequest request, Orchestrator orchestrator) =>
{
    var filePath = Path.Combine(docsDir, request.FileName);
    if (!File.Exists(filePath))
    {
        return Results.NotFound("File not found.");
    }

    string text = string.Empty;
    var ext = Path.GetExtension(request.FileName).ToLowerInvariant();
    if (ext == ".pdf")
    {
        using var pdf = PdfDocument.Open(filePath);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            sb.AppendLine(page.Text);
        }
        text = sb.ToString();
    }
    else
    {
        text = await File.ReadAllTextAsync(filePath);
    }

    var summary = await orchestrator.SummarizeClaimAsync(text);
    return Results.Ok(new { response = summary });
});

// 6. Extract structured information
app.MapPost("/api/assistant/extract", async (FileRequest request, Orchestrator orchestrator) =>
{
    var filePath = Path.Combine(docsDir, request.FileName);
    if (!File.Exists(filePath))
    {
        return Results.NotFound("File not found.");
    }

    string text = string.Empty;
    var ext = Path.GetExtension(request.FileName).ToLowerInvariant();
    if (ext == ".pdf")
    {
        using var pdf = PdfDocument.Open(filePath);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            sb.AppendLine(page.Text);
        }
        text = sb.ToString();
    }
    else
    {
        text = await File.ReadAllTextAsync(filePath);
    }

    var extraction = await orchestrator.ExtractKeyInfoAsync(text);
    return Results.Ok(extraction);
});

// 7. Decision Support
app.MapPost("/api/assistant/decision", async (DecisionRequest request, Orchestrator orchestrator) =>
{
    var claimPath = Path.Combine(docsDir, request.ClaimFileName);
    var policyPath = Path.Combine(docsDir, request.PolicyFileName);

    if (!File.Exists(claimPath) || !File.Exists(policyPath))
    {
        return Results.NotFound("Claim or policy file not found.");
    }

    string claimText = string.Empty;
    var ext = Path.GetExtension(request.ClaimFileName).ToLowerInvariant();
    if (ext == ".pdf")
    {
        using var pdf = PdfDocument.Open(claimPath);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            sb.AppendLine(page.Text);
        }
        claimText = sb.ToString();
    }
    else
    {
        claimText = await File.ReadAllTextAsync(claimPath);
    }

    var decision = await orchestrator.GetDecisionSupportAsync(request.ClaimFileName, claimText, request.PolicyFileName);
    return Results.Ok(decision);
});

// Fallback to static index.html for SPA
app.MapFallbackToFile("index.html");

app.Run();

// DTOs
public class QueryRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string? Type { get; set; } // "Policy" or "Claim"
}

public class FileRequest
{
    public string FileName { get; set; } = string.Empty;
}

public class DecisionRequest
{
    public string ClaimFileName { get; set; } = string.Empty;
    public string PolicyFileName { get; set; } = string.Empty;
}
