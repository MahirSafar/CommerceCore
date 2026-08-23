using CommerceCore.Domain.Catalog.Products.ValueObjects;

namespace CommerceCore.Domain.UnitTests.Catalog.Products.ValueObjects;

public sealed class ProductVariantIdTests
{
    [Fact]
    public void New_CreatesNonEmptyIdentifier() =>
        Assert.NotEqual(Guid.Empty, ProductVariantId.New().Value);

    [Fact]
    public void From_WithEmptyGuid_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(
            () => ProductVariantId.From(Guid.Empty));
}