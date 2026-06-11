using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace InsuranceAssistant
{
    public class ExtractionResult
    {
        public string ClaimantName { get; set; } = "Unknown";
        public string PolicyNumber { get; set; } = "Unknown";
        public string DateOfLoss { get; set; } = "Unknown";
        public string ClaimAmount { get; set; } = "Unknown";
        public string IncidentDescription { get; set; } = "Unknown";
        public List<string> KeyDetails { get; set; } = new();
    }

    public class DecisionResult
    {
        public string Recommendation { get; set; } = "Investigate"; // Approve, Deny, Investigate
        public double Confidence { get; set; } = 0.5;
        public string Reasoning { get; set; } = string.Empty;
        public List<string> PolicyReferences { get; set; } = new();
    }

    public class Orchestrator
    {
        private readonly Kernel _kernel;
        private readonly VectorStore _vectorStore;

        public Orchestrator(VectorStore vectorStore)
        {
            _vectorStore = vectorStore;

            // Build Semantic Kernel targeting local Ollama's OpenAI-compatible API endpoint
            var builder = Kernel.CreateBuilder();
            builder.AddOpenAIChatCompletion(
                modelId: "llama3.2",
                apiKey: "none",
                endpoint: new Uri("http://localhost:11434/v1")
            );

            _kernel = builder.Build();
        }

        public async Task<string> AskQueryAsync(string userQuestion, string? docType = null)
        {
            // Search vector store for relevant documents matching the question
            var searchResults = await _vectorStore.SearchAsync(userQuestion, docType, limit: 4);
            
            var context = string.Join("\n\n", searchResults.Select(r => 
                $"[Source: {r.Record.SourceFile} (Type: {r.Record.Type})]\n{r.Record.Content}"));

            var chat = _kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            
            history.AddSystemMessage("You are a helpful Insurance Claims & Policy Assistant. " +
                "Use the provided context to answer the user's question accurately. If you don't know the answer or if it's not in the context, " +
                "state that, but do your best to answer using the context. Cite the source files you used.");

            if (!string.IsNullOrWhiteSpace(context))
            {
                history.AddSystemMessage($"Here is the retrieved context from uploaded files:\n{context}");
            }

            history.AddUserMessage(userQuestion);

            var response = await chat.GetChatMessageContentAsync(history);
            return response.Content ?? "No response generated.";
        }

        public async Task<string> SummarizeClaimAsync(string claimText)
        {
            var chat = _kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();

            history.AddSystemMessage("You are an expert Insurance Claims Adjuster. " +
                "Summarize the provided insurance claim incident report or documentation. " +
                "Ensure you extract: 1) Executive Summary, 2) Timeline of Events, 3) Key Claims/Damages, 4) Next Steps. " +
                "Format the response beautifully in clean Markdown with clear headers.");

            history.AddUserMessage(claimText);

            var response = await chat.GetChatMessageContentAsync(history);
            return response.Content ?? "Unable to generate summary.";
        }

        public async Task<ExtractionResult> ExtractKeyInfoAsync(string documentText)
        {
            var chat = _kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();

            history.AddSystemMessage("You are an AI assistant that extracts key structured metadata from insurance policy or claim documents. " +
                "Extract the following: Claimant Name, Policy Number, Date of Loss/Incident, Claim/Estimate Amount, Incident Description, and list any 3-5 key outstanding details. " +
                "You MUST respond ONLY with a valid JSON object matching the schema below. No conversational text, no markdown wrapping other than raw json. " +
                "JSON Schema:\n" +
                "{\n" +
                "  \"ClaimantName\": \"str\",\n" +
                "  \"PolicyNumber\": \"str\",\n" +
                "  \"DateOfLoss\": \"str\",\n" +
                "  \"ClaimAmount\": \"str\",\n" +
                "  \"IncidentDescription\": \"str\",\n" +
                "  \"KeyDetails\": [\"str\"]\n" +
                "}");

            history.AddUserMessage(documentText);

            var response = await chat.GetChatMessageContentAsync(history);
            var content = response.Content ?? string.Empty;

            // Strip markdown code block wrapping if present
            if (content.StartsWith("```"))
            {
                content = content.Trim('`').Replace("json\n", "").Replace("json", "").Trim();
            }

            try
            {
                var result = JsonSerializer.Deserialize<ExtractionResult>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return result ?? new ExtractionResult();
            }
            catch
            {
                // Fallback parsing if JSON deserialization fails
                return new ExtractionResult
                {
                    IncidentDescription = "Failed to parse structured JSON. Raw output: " + content
                };
            }
        }

        public async Task<DecisionResult> GetDecisionSupportAsync(string claimFileName, string claimText, string policyFileName)
        {
            // Search vector store for clauses in policy file matching the claim context
            var searchQuery = $"Evaluate claim details from {claimFileName} and match policy rules in {policyFileName}";
            var searchResults = await _vectorStore.SearchAsync(searchQuery, "Policy", limit: 6);
            
            // Filter search results specifically to the selected policy file if possible
            var policyContextList = searchResults
                .Where(r => r.Record.SourceFile.Equals(policyFileName, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Record.Content)
                .ToList();

            // If we didn't find specific matches, get generic policy snippets from this file
            if (!policyContextList.Any())
            {
                policyContextList = _vectorStore.ListDocuments()
                    .Where(r => r.SourceFile.Equals(policyFileName, StringComparison.OrdinalIgnoreCase))
                    .Select(r => r.Content)
                    .Take(5)
                    .ToList();
            }

            var policyContext = string.Join("\n\n", policyContextList);

            var chat = _kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();

            history.AddSystemMessage("You are an expert Insurance Coverage Analyst and Decision Support System. " +
                "Evaluate the claims details against the rules and guidelines specified in the policy terms. " +
                "You must recommend one of the following decisions:\n" +
                "- Approve: If the claim is clearly covered under the guidelines.\n" +
                "- Deny: If the claim is clearly excluded or exceeds limits.\n" +
                "- Investigate: If coverage is ambiguous or missing documentation.\n\n" +
                "You MUST respond ONLY with a valid JSON object matching the schema below. No conversational text or markdown packaging.\n" +
                "JSON Schema:\n" +
                "{\n" +
                "  \"Recommendation\": \"Approve|Deny|Investigate\",\n" +
                "  \"Confidence\": 0.85,\n" +
                "  \"Reasoning\": \"detailed textual explanation citing policies\",\n" +
                "  \"PolicyReferences\": [\"specific clause or page cite\"]\n" +
                "}");

            history.AddUserMessage($"Policy Rules Context:\n{policyContext}\n\nClaim Incident Details:\n{claimText}");

            var response = await chat.GetChatMessageContentAsync(history);
            var content = response.Content ?? string.Empty;

            if (content.StartsWith("```"))
            {
                content = content.Trim('`').Replace("json\n", "").Replace("json", "").Trim();
            }

            try
            {
                var result = JsonSerializer.Deserialize<DecisionResult>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return result ?? new DecisionResult { Reasoning = "Failed to deserialize JSON response." };
            }
            catch
            {
                return new DecisionResult
                {
                    Recommendation = "Investigate",
                    Confidence = 0.5,
                    Reasoning = "Failed to parse structured JSON from LLM: " + content
                };
            }
        }
    }
}
