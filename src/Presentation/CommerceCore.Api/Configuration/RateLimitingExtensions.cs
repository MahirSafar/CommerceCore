using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace CommerceCore.Api.Configuration;

internal static class RateLimitingExtensions
{
    public static void AddRateLimiting(this IHostApplicationBuilder builder)
    {
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode =
                StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter =
                PartitionedRateLimiter.Create<HttpContext, string>(
                    httpContext =>
                    {
                        if (!httpContext.Request.Path.StartsWithSegments("/api"))
                        {
                            return RateLimitPartition.GetNoLimiter(
                                "non-api");
                        }

                        bool isReadRequest =
                            HttpMethods.IsGet(httpContext.Request.Method) ||
                            HttpMethods.IsHead(httpContext.Request.Method) ||
                            HttpMethods.IsOptions(httpContext.Request.Method);

                        string clientKey = "unknown";

                        if (httpContext.User.Identity?.IsAuthenticated == true)
                        {
                            string? sub = httpContext.User.FindFirst("sub")?.Value;
                            string? clientId = httpContext.User.FindFirst("client_id")?.Value;

                            if (!string.IsNullOrEmpty(sub))
                            {
                                clientKey = $"user:{sub}";
                            }
                            else if (!string.IsNullOrEmpty(clientId))
                            {
                                clientKey = $"client:{clientId}";
                            }
                        }

                        if (clientKey == "unknown")
                        {
                            clientKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                        }

                        string partitionKey =
                            $"{(isReadRequest ? "read" : "write")}:{clientKey}";

                        return RateLimitPartition
                            .GetSlidingWindowLimiter(
                                partitionKey,
                                _ => new SlidingWindowRateLimiterOptions
                                {
                                    PermitLimit = isReadRequest ? 300 : 60,
                                    Window = TimeSpan.FromMinutes(1),
                                    SegmentsPerWindow = 6,
                                    QueueProcessingOrder =
                                        QueueProcessingOrder.OldestFirst,
                                    QueueLimit = 0,
                                    AutoReplenishment = true
                                });
                    });

            options.OnRejected = WriteRateLimitProblemAsync;
        });
    }

    private static async ValueTask WriteRateLimitProblemAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        HttpResponse response = context.HttpContext.Response;

        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.ContentType = "application/problem+json";

        if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter,
                out TimeSpan retryAfter))
        {
            response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds)
                    .ToString(CultureInfo.InvariantCulture);
        }

        ProblemDetails problem = new()
        {
            Type = "/problems/rate-limit-exceeded",
            Title = "Too many requests.",
            Detail = "Request limit exceeded. Retry later.",
            Status = StatusCodes.Status429TooManyRequests,
            Instance = context.HttpContext.Request.Path
        };

        problem.Extensions["traceId"] =
            context.HttpContext.TraceIdentifier;

        await response.WriteAsJsonAsync(
            problem,
            cancellationToken: cancellationToken);
    }
}
