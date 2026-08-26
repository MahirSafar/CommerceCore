using CommerceCore.Api.Common.Errors;
using CommerceCore.Api.Common.HealthChecks;
using CommerceCore.Api.Common.Security;
using CommerceCore.Api.Endpoints.V1.Products;
using CommerceCore.Api.Endpoints.V1.ProductTypes;
using CommerceCore.Api.Identity;
using CommerceCore.Application;
using CommerceCore.Application.Catalog.Products.Commands.CreateProduct;
using CommerceCore.Application.Common.Abstractions;
using CommerceCore.Application.Common.Behaviors;
using CommerceCore.Infrastructure.Common.Time;
using CommerceCore.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Globalization;
using System.Threading.RateLimiting;


var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString(
    "CommerceCoreDatabase") ?? throw new InvalidOperationException("Connection string 'CommerceCoreDatabase' was not found");

string? allowedHosts = builder.Configuration["AllowedHosts"];

if (!builder.Environment.IsDevelopment() &&
    (string.IsNullOrWhiteSpace(allowedHosts) ||
     allowedHosts
         .Split(
             ';',
             StringSplitOptions.TrimEntries |
             StringSplitOptions.RemoveEmptyEntries)
         .Any(host => host == "*")))
{
    throw new InvalidOperationException(
        "AllowedHosts must contain explicit host names outside Development.");
}

string? authenticationAuthority = builder.Configuration[
    "Authentication:Schemes:Bearer:Authority"];

string? authenticationAudience = builder.Configuration[
    "Authentication:Schemes:Bearer:Audience"];

if (!builder.Environment.IsDevelopment() &&
    (string.IsNullOrWhiteSpace(authenticationAuthority) ||
     string.IsNullOrWhiteSpace(authenticationAudience)))
{
    throw new InvalidOperationException(
        "Bearer Authority and Audience must be configured outside Development.");
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

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

                string clientKey =
                    httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

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

if (!string.IsNullOrWhiteSpace(
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    const string serviceName = "CommerceCore.Api";

    string serviceVersion = typeof(Program)
        .Assembly
        .GetName()
        .Version?
        .ToString() ?? "unknown";

    ResourceBuilder resourceBuilder = ResourceBuilder
        .CreateDefault()
        .AddService(
            serviceName: serviceName,
            serviceVersion: serviceVersion)
        .AddAttributes(
            new Dictionary<string, object>
            {
                ["deployment.environment.name"] =
                    builder.Environment.EnvironmentName
            });

    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.SetResourceBuilder(resourceBuilder);
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
        logging.ParseStateValues = true;
        logging.AddOtlpExporter();
    });

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(
                serviceName: serviceName,
                serviceVersion: serviceVersion)
            .AddAttributes(
                new Dictionary<string, object>
                {
                    ["deployment.environment.name"] =
                        builder.Environment.EnvironmentName
                }))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter())
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddMeter(
                "Microsoft.AspNetCore.Hosting",
                "Microsoft.AspNetCore.Server.Kestrel",
                "System.Runtime")
            .AddOtlpExporter());
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

AuthorizationPolicy fallbackPolicy =
    new AuthorizationPolicyBuilder(
            JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(fallbackPolicy)
    .AddPolicy(
        AuthorizationPolicies.CatalogRead,
        policy => policy
            .AddAuthenticationSchemes(
                JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                HasScope(context, "catalog.read")))
    .AddPolicy(
        AuthorizationPolicies.CatalogManage,
        policy => policy
            .AddAuthenticationSchemes(
                JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                HasScope(context, "catalog.manage")))
    .AddPolicy(
        AuthorizationPolicies.CatalogSchemaManage,
        policy => policy
            .AddAuthenticationSchemes(
                JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                HasScope(
                    context,
                    "catalog.schema.manage")));

builder.Services.AddOpenApi();

builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddApplication();
builder.Services.AddMediator(options =>
{
    options.Assemblies = [typeof(CreateProductCommand)];
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
});
builder.Services.AddPersistence(connectionString);

builder.Services.AddHealthChecks()
    .AddCheck<PostgreSqlHealthCheck>(
        "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = _ => false
    })
    .AllowAnonymous();

app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    })
    .AllowAnonymous();

app.MapProductEndpoints();
app.MapProductTypeEndpoints();

app.Run();

static async ValueTask WriteRateLimitProblemAsync(
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

static bool HasScope(
    AuthorizationHandlerContext context,
    string requiredScope)
{
    return context.User
        .FindAll("scope")
        .SelectMany(claim => claim.Value.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries))
        .Contains(requiredScope, StringComparer.Ordinal);
}