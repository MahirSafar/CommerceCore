using CommerceCore.Application.Catalog.ProductTypes.Queries.GetProductTypeSchema;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Platform.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.ProductTypes;

[Collection(nameof(PostgreSqlCollection))]
public sealed class GetProductTypeSchemaQueryIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Handle_ReturnsInheritedAttributeAndOptions()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        TestTenantContext context = fixture.Services
            .GetRequiredService<TestTenantContext>();

        try
        {
            TenantId tenantId = await fixture.CreateTenantAsync(
                cancellationToken);

            context.SetTenant(tenantId);

            Guid childId = await SeedHierarchyAsync(
                tenantId,
                cancellationToken);

            ProductTypeSchemaResult? result = await ReadAsync(
                childId,
                cancellationToken);

            Assert.NotNull(result);
            Assert.Equal(childId, result.ProductTypeId);
            Assert.True(result.EffectiveSchemaVersion > 0);

            ProductTypeAttributeSchema attribute =
                Assert.Single(result.Attributes);

            Assert.Equal("color", attribute.Key);
            Assert.Equal("single_select", attribute.DataType);
            Assert.Equal("variant_option", attribute.Scope);
            Assert.False(attribute.IsRequired);
            Assert.Equal("enforced", attribute.EnforcementStatus);
            Assert.False(attribute.IsDeprecated);

            Assert.Null(attribute.MinimumLength);
            Assert.Null(attribute.MaximumLength);
            Assert.Null(attribute.MinimumValue);
            Assert.Null(attribute.MaximumValue);
            Assert.Null(attribute.MeasurementUnitFamily);

            ProductTypeAttributeOptionSchema option =
                Assert.Single(attribute.Options);

            Assert.Equal("black", option.Code);
            Assert.False(option.IsDeprecated);
        }
        finally
        {
            context.Clear();
        }
    }

    [Fact]
    public async Task Handle_ForeignTenantCannotReadSchema()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        TestTenantContext context = fixture.Services
            .GetRequiredService<TestTenantContext>();

        try
        {
            TenantId tenantA = await fixture.CreateTenantAsync(
                cancellationToken);

            TenantId tenantB = await fixture.CreateTenantAsync(
                cancellationToken);

            context.SetTenant(tenantA);

            Guid childId = await SeedHierarchyAsync(
                tenantA,
                cancellationToken);

            ProductTypeSchemaResult? ownSchema = await ReadAsync(
                childId,
                cancellationToken);

            Assert.NotNull(ownSchema);

            context.SetTenant(tenantB);

            ProductTypeSchemaResult? foreignSchema = await ReadAsync(
                childId,
                cancellationToken);

            Assert.Null(foreignSchema);

            context.SetTenant(tenantA);

            ProductTypeSchemaResult? repeatedSchema = await ReadAsync(
                childId,
                cancellationToken);

            Assert.NotNull(repeatedSchema);
            Assert.Equal(
                ownSchema.EffectiveSchemaVersion,
                repeatedSchema.EffectiveSchemaVersion);
        }
        finally
        {
            context.Clear();
        }
    }

    [Fact]
    public async Task Handle_MissingSchemaReturnsNull()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        TestTenantContext context = fixture.Services
            .GetRequiredService<TestTenantContext>();

        try
        {
            fixture.SetTenantForCurrentTest();

            ProductTypeSchemaResult? result = await ReadAsync(
                Guid.NewGuid(),
                cancellationToken);

            Assert.Null(result);
        }
        finally
        {
            context.Clear();
        }
    }

    [Fact]
    public async Task Handle_WithoutTenantRejectsRequest()
    {
        fixture.Services
            .GetRequiredService<TestTenantContext>()
            .Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ReadAsync(
                Guid.NewGuid(),
                TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", false)]
    [InlineData("11111111-1111-4111-8111-111111111111", true)]
    public void Validator_ChecksProductTypeId(
        string value,
        bool expectedValid)
    {
        var validator = new GetProductTypeSchemaQueryValidator();

        var result = validator.Validate(
            new GetProductTypeSchemaQuery(Guid.Parse(value)));

        Assert.Equal(expectedValid, result.IsValid);
    }

    private async Task<ProductTypeSchemaResult?> ReadAsync(
        Guid productTypeId,
        CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        var reader = scope.ServiceProvider
            .GetRequiredService<IProductTypeEffectiveSchemaReader>();

        var context = scope.ServiceProvider
            .GetRequiredService<ITenantContext>();

        var handler = new GetProductTypeSchemaQueryHandler(
            reader,
            context);

        ProductTypeSchemaResult? result = await handler.Handle(
            new GetProductTypeSchemaQuery(productTypeId),
            cancellationToken);

        var database = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        Assert.Empty(database.ChangeTracker.Entries());

        return result;
    }

    private async Task<Guid> SeedHierarchyAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        var database = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        var coordinator = scope.ServiceProvider
            .GetRequiredService<IProductTypeSchemaCoordinator>();

        ProductType root = ProductType.CreateRoot(
            tenantId,
            ProductTypeCode.Create($"root_{Guid.NewGuid():N}"),
            isAssignable: false);

        AttributeDefinition color = root.DefineAttribute(
            AttributeKey.Create("color"),
            AttributeDataType.SingleSelect,
            AttributeScope.VariantOption,
            isRequired: false,
            displayOrder: 0);

        root.AddAttributeOption(
            color.Id,
            AttributeOptionCode.Create("black"),
            displayOrder: 0);

        await coordinator.ExecuteCreationAsync(
            root.Id,
            root.ParentProductTypeId,
            async token =>
            {
                database.ProductTypes.Add(root);
                await database.SaveChangesAsync(token);
            },
            cancellationToken);

        ProductType child = ProductType.CreateChild(
            tenantId,
            root.Id,
            ProductTypeCode.Create($"child_{Guid.NewGuid():N}"),
            isAssignable: true);

        await coordinator.ExecuteCreationAsync(
            child.Id,
            child.ParentProductTypeId,
            async token =>
            {
                database.ProductTypes.Add(child);
                await database.SaveChangesAsync(token);
            },
            cancellationToken);

        return child.Id.Value;
    }
}
