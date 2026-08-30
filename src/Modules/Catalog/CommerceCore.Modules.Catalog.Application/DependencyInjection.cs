using CommerceCore.Domain.Catalog.ProductTypes.Schema;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Application.Catalog;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogApplication(
        this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(
            typeof(DependencyInjection).Assembly,
            ServiceLifetime.Scoped);

        services.AddSingleton<ICatalogSchemaValidator, CatalogSchemaValidator>();

        return services;
    }
}