using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.ProductTypes;

[Collection(nameof(PostgreSqlCollection))]
public sealed class ProductTypePersistenceIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task SaveChanges_PersistsHierarchyAttributesOptionsAndLtreePaths()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        TenantId tenantId = await fixture.CreateTenantAsync(cancellationToken);
        var tenantContext = fixture.Services.GetRequiredService<TestTenantContext>();
        tenantContext.SetTenant(tenantId);

        await using var scope = fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        ProductType root = ProductType.CreateRoot(
            tenantId,
            ProductTypeCode.Create("electronics"));

        dbContext.ProductTypes.Add(root);
        await dbContext.SaveChangesAsync(cancellationToken);

        ProductType child = ProductType.CreateChild(
            tenantId,
            root.Id,
            ProductTypeCode.Create("gaming_laptop"));

        AttributeDefinition color = child.DefineAttribute(
            AttributeKey.Create("color"),
            AttributeDataType.SingleSelect,
            AttributeScope.VariantOption,
            isRequired: true,
            displayOrder: 0);

        child.AddAttributeOption(
            color.Id,
            AttributeOptionCode.Create("space-black"),
            displayOrder: 0);

        dbContext.ProductTypes.Add(child);
        await dbContext.SaveChangesAsync(cancellationToken);

        FormattableString rootPathQuery = $"""
            SELECT path::text AS "Value"
            FROM catalog.product_types
            WHERE id = {root.Id.Value}
            """;

        FormattableString childPathQuery = $"""
            SELECT path::text AS "Value"
            FROM catalog.product_types
            WHERE id = {child.Id.Value}
            """;

        string rootPath = await dbContext.Database
            .SqlQuery<string>(rootPathQuery)
            .SingleAsync(cancellationToken);

        string childPath = await dbContext.Database
            .SqlQuery<string>(childPathQuery)
            .SingleAsync(cancellationToken);

        Assert.Equal("electronics", rootPath);
        Assert.Equal("electronics.gaming_laptop", childPath);

        dbContext.ChangeTracker.Clear();

        ProductType persistedChild = await dbContext.ProductTypes
            .Include(productType => productType.AttributeDefinitions)
            .ThenInclude(definition => definition.Options)
            .SingleAsync(
                productType => productType.Id == child.Id,
                cancellationToken);

        AttributeDefinition persistedColor = Assert.Single(persistedChild.AttributeDefinitions);

        Assert.Equal("color", persistedColor.Key.Value);
        Assert.Equal(AttributeDataType.SingleSelect, persistedColor.DataType);
        Assert.Equal(AttributeScope.VariantOption, persistedColor.Scope);
        Assert.Equal(
            AttributeEnforcementStatus.Draft,
            persistedColor.EnforcementStatus);

        AttributeOption persistedOption = Assert.Single(persistedColor.Options);

        Assert.Equal("space-black", persistedOption.Code.Value);
        Assert.Equal("integration-test", persistedChild.CreatedBy);
    }

    [Fact]
    public async Task SchemaRevisionSequence_ReturnsPositiveRevision()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using var scope = fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        FormattableString query = $"""
            SELECT nextval('catalog.schema_revision_seq') AS "Value"
            """;

        long revision = await dbContext.Database
            .SqlQuery<long>(query)
            .SingleAsync(cancellationToken);

        Assert.True(revision > 0);
    }
}