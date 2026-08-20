using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Persistence.IntegrationTests.Infrastructure;

internal static class SeededCatalogIds
{
    public static readonly ProductTypeId LegacyUnclassifiedProductTypeId =
        ProductTypeId.From(
            Guid.Parse("018f20f0-0000-7000-8000-000000000001"));
}
