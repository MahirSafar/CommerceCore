using CommerceCore.Api.Common.Errors;
using CommerceCore.Api.Common.Security;
using CommerceCore.Api.Configuration;
using CommerceCore.Api.Endpoints.V1.Products;
using CommerceCore.Api.Endpoints.V1.ProductTypes;
using CommerceCore.Api.Identity;
using CommerceCore.Application;
using CommerceCore.Application.Catalog.Products.Commands.CreateProduct;
using CommerceCore.Application.Common.Abstractions;
using CommerceCore.Application.Common.Behaviors;
using CommerceCore.Infrastructure.Common.Time;
using CommerceCore.Persistence;

using CommerceCore.Platform.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.ValidateConfiguration();

var connectionString = builder.Configuration.GetConnectionString("CommerceCoreDatabase")!;

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

builder.AddRateLimiting();
builder.AddObservability();
builder.AddSecurity();
builder.AddHealthChecksConfig();

builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddPlatformTenantServices();

builder.Services.AddApplication();
builder.Services.AddMediator(options =>
{
    options.Assemblies = [typeof(CreateProductCommand)];
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
});
builder.Services.AddPersistence(connectionString);

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
app.UsePlatformTenantResolution();
app.UseRateLimiter();
app.UseAuthorization();

app.MapHealthCheckEndpoints();
app.MapProductEndpoints();
app.MapProductTypeEndpoints();

app.Run();

public partial class Program { }
