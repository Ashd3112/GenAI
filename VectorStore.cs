using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace InsuranceAssistant
{
    public class VectorRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SourceFile { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public string Type { get; set; } = "General"; // "Policy" or "Claim"
        public int ChunkIndex { get; set; }
    }

    public class VectorStore
    {
        private readonly HttpClient _httpClient;
        private readonly string _connectionString = "Server=localhost;Database=InsuranceClaimsAssistant;Trusted_Connection=True;TrustServerCertificate=True;";
        private readonly string _masterConnectionString = "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;";

        public VectorStore(HttpClient httpClient)
        {
            _httpClient = httpClient;
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            try
            {
                // Ensure Database Exists
                using (var masterConn = new SqlConnection(_masterConnectionString))
                {
                    masterConn.Open();
                    var checkDbCmd = new SqlCommand("IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'InsuranceClaimsAssistant') CREATE DATABASE InsuranceClaimsAssistant;", masterConn);
                    checkDbCmd.ExecuteNonQuery();
                }

                // Ensure Tables Exist
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    var createTablesCmdText = @"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Documents')
                        BEGIN
                            CREATE TABLE Documents (
                                FileName NVARCHAR(255) PRIMARY KEY,
                                Type NVARCHAR(50) NOT NULL,
                                UploadedAt DATETIME NOT NULL DEFAULT GETDATE(),
                                Content NVARCHAR(MAX) NOT NULL
                            );
                        END

                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'VectorRecords')
                        BEGIN
                            CREATE TABLE VectorRecords (
                                Id NVARCHAR(50) PRIMARY KEY,
                                SourceFile NVARCHAR(255) NOT NULL FOREIGN KEY REFERENCES Documents(FileName) ON DELETE CASCADE,
                                Content NVARCHAR(MAX) NOT NULL,
                                EmbeddingJson NVARCHAR(MAX) NOT NULL,
                                Type NVARCHAR(50) NOT NULL,
                                ChunkIndex INT NOT NULL
                            );
                        END";
                    
                    var createTablesCmd = new SqlCommand(createTablesCmdText, conn);
                    createTablesCmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing SQL Server database: {ex.Message}");
            }
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("http://localhost:11434/api/embeddings", new
                {
                    model = "nomic-embed-text",
                    prompt = text
                });

                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
                return result?.Embedding ?? Array.Empty<float>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating embedding: {ex.Message}");
                return Array.Empty<float>();
            }
        }

        public async Task IndexDocumentAsync(string fileName, string text, string type)
        {
            var chunks = ChunkText(text, 800, 150);

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // Start a transaction
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // Insert or Update Document metadata
                        var upsertDocCmd = new SqlCommand(@"
                            IF EXISTS (SELECT 1 FROM Documents WHERE FileName = @FileName)
                                UPDATE Documents SET Type = @Type, Content = @Content WHERE FileName = @FileName
                            ELSE
                                INSERT INTO Documents (FileName, Type, Content) VALUES (@FileName, @Type, @Content)", 
                            conn, transaction);
                        
                        upsertDocCmd.Parameters.AddWithValue("@FileName", fileName);
                        upsertDocCmd.Parameters.AddWithValue("@Type", type);
                        upsertDocCmd.Parameters.AddWithValue("@Content", text);
                        await upsertDocCmd.ExecuteNonQueryAsync();

                        // Clean existing vector chunks for this file
                        var deleteChunksCmd = new SqlCommand("DELETE FROM VectorRecords WHERE SourceFile = @SourceFile", conn, transaction);
                        deleteChunksCmd.Parameters.AddWithValue("@SourceFile", fileName);
                        await deleteChunksCmd.ExecuteNonQueryAsync();

                        // Process and insert chunks
                        for (int i = 0; i < chunks.Count; i++)
                        {
                            var chunk = chunks[i];
                            var embedding = await GetEmbeddingAsync(chunk);
                            if (embedding.Length > 0)
                            {
                                var insertChunkCmd = new SqlCommand(@"
                                    INSERT INTO VectorRecords (Id, SourceFile, Content, EmbeddingJson, Type, ChunkIndex) 
                                    VALUES (@Id, @SourceFile, @Content, @EmbeddingJson, @Type, @ChunkIndex)", 
                                    conn, transaction);
                                
                                insertChunkCmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
                                insertChunkCmd.Parameters.AddWithValue("@SourceFile", fileName);
                                insertChunkCmd.Parameters.AddWithValue("@Content", chunk);
                                insertChunkCmd.Parameters.AddWithValue("@EmbeddingJson", JsonSerializer.Serialize(embedding));
                                insertChunkCmd.Parameters.AddWithValue("@Type", type);
                                insertChunkCmd.Parameters.AddWithValue("@ChunkIndex", i);
                                
                                await insertChunkCmd.ExecuteNonQueryAsync();
                            }
                        }

                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public List<VectorRecord> ListDocuments()
        {
            var records = new List<VectorRecord>();
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("SELECT Id, SourceFile, Content, EmbeddingJson, Type, ChunkIndex FROM VectorRecords", conn);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            records.Add(new VectorRecord
                            {
                                Id = reader.GetString(0),
                                SourceFile = reader.GetString(1),
                                Content = reader.GetString(2),
                                Embedding = JsonSerializer.Deserialize<float[]>(reader.GetString(3)) ?? Array.Empty<float>(),
                                Type = reader.GetString(4),
                                ChunkIndex = reader.GetInt32(5)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing documents from SQL Server: {ex.Message}");
            }
            return records;
        }

        public void DeleteDocument(string fileName)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("DELETE FROM Documents WHERE FileName = @FileName", conn);
                    cmd.Parameters.AddWithValue("@FileName", fileName);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting document: {ex.Message}");
            }
        }

        public async Task<List<(VectorRecord Record, double Similarity)>> SearchAsync(string query, string? type = null, int limit = 5)
        {
            var queryEmbedding = await GetEmbeddingAsync(query);
            if (queryEmbedding.Length == 0) return new List<(VectorRecord Record, double Similarity)>();

            var allRecords = ListDocuments();
            var results = new List<(VectorRecord Record, double Similarity)>();
            var filteredRecords = type == null ? allRecords : allRecords.Where(r => r.Type.Equals(type, StringComparison.OrdinalIgnoreCase));

            foreach (var record in filteredRecords)
            {
                var similarity = CosineSimilarity(queryEmbedding, record.Embedding);
                results.Add((Record: record, Similarity: similarity));
            }

            return results
                .OrderByDescending(r => r.Similarity)
                .Take(limit)
                .ToList();
        }

        private static List<string> ChunkText(string text, int chunkSize, int overlap)
        {
            var chunks = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return chunks;

            int index = 0;
            while (index < text.Length)
            {
                if (index + chunkSize >= text.Length)
                {
                    chunks.Add(text.Substring(index).Trim());
                    break;
                }

                int end = index + chunkSize;
                int lastSpace = text.LastIndexOf(' ', end, chunkSize / 2);
                if (lastSpace > index)
                {
                    end = lastSpace;
                }

                chunks.Add(text.Substring(index, end - index).Trim());
                index = end - overlap;
                if (index < 0) index = 0;
                if (index >= text.Length) break;
            }

            return chunks;
        }

        private static double CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length || vectorA.Length == 0) return 0;

            double dotProduct = 0;
            double normA = 0;
            double normB = 0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                normA += vectorA[i] * vectorA[i];
                normB += vectorB[i] * vectorB[i];
            }

            if (normA == 0 || normB == 0) return 0;
            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        private class OllamaEmbeddingResponse
        {
            [JsonPropertyName("embedding")]
            public float[] Embedding { get; set; } = Array.Empty<float>();
        }
    }
}
