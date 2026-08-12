using System;
using CommerceCore.Domain.Common.Localization;
using Xunit;

namespace CommerceCore.Domain.Tests.Catalog.Products;

public class LanguageCodeTests
{
    [Fact]
    public void Create_WithPtBr_ShouldNormalizeToPtBR()
    {
        var code = LanguageCode.Create("pt-br");

        Assert.Equal("pt-BR", code.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid@")] 
    [InlineData("abc-$$$$")]
    public void Create_InvalidFormats_ShouldThrowArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => LanguageCode.Create(input));
    }
}
