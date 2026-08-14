using CommerceCore.Api.Identity;
using CommerceCore.Application.Common.Abstractions;
using CommerceCore.Infrastructure.Common.Time;
using CommerceCore.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString(
    "CommerceCoreDatabase") ?? throw new InvalidOperationException("Connection string 'CommerceCoreDatabase' was not found");

builder.Services.AddOpenApi();

builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddPersistence(connectionString);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();

