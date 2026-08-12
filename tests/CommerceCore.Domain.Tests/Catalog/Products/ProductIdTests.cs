using System;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using Xunit;

namespace CommerceCore.Domain.Tests.Catalog.Products;

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
}
