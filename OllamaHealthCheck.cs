using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InsuranceAssistant
{
    public class OllamaHealthCheck : IHealthCheck
    {
        private readonly HttpClient _httpClient;
        private const string OllamaUrl = "http://localhost:11434";

        public OllamaHealthCheck(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // Send a quick GET request to Ollama's base URL
                var response = await _httpClient.GetAsync(OllamaUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Healthy("Ollama service is running and healthy.");
                }
                
                return HealthCheckResult.Degraded($"Ollama service returned unexpected status: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Ollama service is unreachable.", ex);
            }
        }
    }
}
