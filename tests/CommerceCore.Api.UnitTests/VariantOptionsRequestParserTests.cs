using System.Text.Json;
using CommerceCore.Api.Endpoints.V1.Products;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using FluentValidation;

namespace CommerceCore.Api.UnitTests;

public sealed class VariantOptionsRequestParserTests
{
    [Fact]
    public void Parse_WithSingleSelectOptions_CreatesAttributeValueBag()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "color": {
                "t": "singleSelect",
                "v": "space-black"
              },
              "storage": {
                "t": "singleSelect",
                "v": "512gb"
              }
            }
            """);

        AttributeValueBag result = VariantOptionsRequestParser.Parse(
            document.RootElement);

        Assert.Equal(2, result.Count);

        Assert.Equal(
            "space-black",
            Assert.IsType<AttributeValue.SingleSelect>(
                result.Values[AttributeKey.Create("color")]).OptionCode);

        Assert.Equal(
            "512gb",
            Assert.IsType<AttributeValue.SingleSelect>(
                result.Values[AttributeKey.Create("storage")]).OptionCode);
    }

    [Fact]
    public void Parse_WithNonSingleSelectTag_ThrowsValidationException()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "color": {
                "t": "text",
                "v": "space-black"
              }
            }
            """);

        ValidationException exception = Assert.Throws<
            ValidationException>(() => VariantOptionsRequestParser.Parse(
                document.RootElement));

        Assert.Contains(
            exception.Errors,
            error => error.ErrorCode ==
                "options.variant_option_must_be_single_select");
    }

    [Fact]
    public void Parse_WithUnexpectedTaggedObjectMember_ThrowsValidationException()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "color": {
                "t": "singleSelect",
                "v": "space-black",
                "other": true
              }
            }
            """);

        ValidationException exception = Assert.Throws<
            ValidationException>(() => VariantOptionsRequestParser.Parse(
                document.RootElement));

        Assert.Contains(
            exception.Errors,
            error => error.ErrorCode ==
                "options.value_invalid_shape");
    }

    [Fact]
    public void Parse_WhenOptionsIsNotObject_ThrowsValidationException()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            [
              "space-black"
            ]
            """);

        ValidationException exception = Assert.Throws<
            ValidationException>(() => VariantOptionsRequestParser.Parse(
                document.RootElement));

        Assert.Contains(
            exception.Errors,
            error => error.ErrorCode == "options.must_be_object");
    }
}