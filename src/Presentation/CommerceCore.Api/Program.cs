using CommerceCore.Api.Common.Errors;
using CommerceCore.Api.Endpoints.V1.Products;
using CommerceCore.Api.Endpoints.V1.ProductTypes;
using CommerceCore.Api.Identity;
using CommerceCore.Application;
using CommerceCore.Application.Catalog.Products.Commands.CreateProduct;
using CommerceCore.Application.Common.Abstractions;
using CommerceCore.Application.Common.Behaviors;
using CommerceCore.Infrastructure.Common.Time;
using CommerceCore.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString(
    "CommerceCoreDatabase") ?? throw new InvalidOperationException("Connection string 'CommerceCoreDatabase' was not found");

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

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapProductEndpoints();
app.MapProductTypeEndpoints();

app.Run();

