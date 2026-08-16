using CommerceCore.Application.Catalog.Products.Commands.ChangeProductName;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.Products;

[Collection(nameof(PostgreSqlCollection))]
public sealed class ChangeProductNameIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Handle_WhenNameChanges_UpdatesJsonbNameAndAuditFields()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        Product product = CreateProduct();

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        ChangeProductNameCommandHandler handler = new(dbContext);

        ChangeProductNameResult? result = await handler.Handle(
            new ChangeProductNameCommand(
                product.Id.Value,
                "az",
                new Dictionary<string, string>
                {
                    ["az"] = "Yeni məhsul adı",
                    ["en"] = "New product name"
                }),
            cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(product.Id.Value, result.ProductId);
        Assert.Equal("az", result.DefaultLanguage);
        Assert.Equal("Yeni məhsul adı", result.NameTranslations["az"]);
        Assert.Equal("New product name", result.NameTranslations["en"]);
        Assert.Equal("Draft", result.Status);

        dbContext.ChangeTracker.Clear();

        Product persistedProduct = await dbContext.Products.SingleAsync(
            entity => entity.Id == product.Id,
            cancellationToken);

        Assert.Equal("az", persistedProduct.Name.DefaultLanguage.Value);
        Assert.Equal(
            "Yeni məhsul adı",
            persistedProduct.Name.Get(LanguageCode.Create("az")));
        Assert.Equal(
            "New product name",
            persistedProduct.Name.Get(LanguageCode.Create("en")));
        Assert.NotNull(persistedProduct.UpdatedAtUtc);
        Assert.Equal("integration-test", persistedProduct.UpdatedBy);
    }

    private static Product CreateProduct()
    {
        LanguageCode language = LanguageCode.Create("en");

        LocalizedText name = LocalizedText.Create(
            language,
            [
                new KeyValuePair<LanguageCode, string>(
                    language,
                    "Original product name")
            ]);

        return Product.Create(
            name,
            Money.Create(100m, "USD"),
            new DateTimeOffset(2026, 8, 16, 9, 0, 0, TimeSpan.Zero));
    }
}