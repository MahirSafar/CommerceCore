using System;
using CommerceCore.Domain.Catalog.Products.ValueObjects;

namespace CommerceCore.Domain.UnitTests.Catalog.Products.ValueObjects;

public class ProductIdTests
{
    [Fact]
    public void New_ShouldCreateNonEmptyUuidV7()
    {
        var productId = ProductId.New();
        var idString = productId.Value.ToString("D");

        Assert.NotEqual(Guid.Empty, productId.Value);
        Assert.Equal('7', idString[14]);
    }

    [Fact]
    public void From_EmptyGuid_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ProductId.From(Guid.Empty));
    }
}
