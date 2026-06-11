using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace InsuranceAssistant
{
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

        public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var startTime = Stopwatch.GetTimestamp();
            var request = context.Request;
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

            // Push traceId to Serilog's LogContext so all log statements in this request scope have it
            using (LogContext.PushProperty("CorrelationId", traceId))
            {
                _logger.LogInformation(
                    "HTTP Request Started: {Method} {Path}{QueryString}",
                    request.Method,
                    request.Path,
                    request.QueryString.HasValue ? request.QueryString.Value : string.Empty);

                try
                {
                    await _next(context);
                }
                finally
                {
                    var elapsed = Stopwatch.GetElapsedTime(startTime);
                    var response = context.Response;

                    _logger.LogInformation(
                        "HTTP Request Completed: {Method} {Path} responded {StatusCode} in {ElapsedMs:F2}ms",
                        request.Method,
                        request.Path,
                        response.StatusCode,
                        elapsed.TotalMilliseconds);
                }
            }
        }
    }
}
