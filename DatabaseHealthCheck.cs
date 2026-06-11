using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InsuranceAssistant
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly string _connectionString = "Server=localhost;Database=InsuranceClaimsAssistant;Trusted_Connection=True;TrustServerCertificate=True;";

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync(cancellationToken);
                    using (var cmd = new SqlCommand("SELECT 1", conn))
                    {
                        await cmd.ExecuteScalarAsync(cancellationToken);
                    }
                }
                return HealthCheckResult.Healthy("SQL Server database connection is healthy.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("SQL Server database connection failed.", ex);
            }
        }
    }
}
