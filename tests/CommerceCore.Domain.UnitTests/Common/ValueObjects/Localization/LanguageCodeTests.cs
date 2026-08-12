using CommerceCore.Domain.Common.ValueObjects.Localization;

namespace CommerceCore.Domain.UnitTests.Common.ValueObjects.Localization;

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
