using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Api.Endpoints.V1.Products;
using FluentValidation;
using System.Text.Json;

namespace CommerceCore.Api.UnitTests;

public sealed class AttributeValueBagRequestParserTests
{
    [Fact]
    public void Parse_WithValidTaggedValues_CreatesTypedBag()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "title": { "t": "text", "v": "Gaming laptop" },
              "ram_gb": { "t": "integer", "v": 16 },
              "screen_size": { "t": "decimal", "v": 15.6 },
              "touch": { "t": "boolean", "v": true },
              "finish": { "t": "singleSelect", "v": "space-black" },
              "ports": {
                "t": "multiSelect",
                "v": [ "usb-c", "hdmi" ]
              }
            }
            """);

        AttributeValueBag result = AttributeValueBagRequestParser.Parse(
            document.RootElement);

        Assert.Equal(6, result.Count);

        Assert.Equal(
            "Gaming laptop",
            Assert.IsType<AttributeValue.Text>(
                result.Values[AttributeKey.Create("title")]).Value);

        Assert.Equal(
            16,
            Assert.IsType<AttributeValue.Integer>(
                result.Values[AttributeKey.Create("ram_gb")]).Value);

        Assert.Equal(
            15.6m,
            Assert.IsType<AttributeValue.Decimal>(
                result.Values[AttributeKey.Create("screen_size")]).Value);

        Assert.True(
            Assert.IsType<AttributeValue.Boolean>(
                result.Values[AttributeKey.Create("touch")]).Value);

        Assert.Equal(
            "space-black",
            Assert.IsType<AttributeValue.SingleSelect>(
                result.Values[AttributeKey.Create("finish")]).OptionCode);

        AttributeValue.MultiSelect ports = Assert.IsType<
            AttributeValue.MultiSelect>(
            result.Values[AttributeKey.Create("ports")]);

        Assert.Equal(["hdmi", "usb-c"], ports.OptionCodes);
    }

    [Fact]
    public void Parse_WithUnknownTag_ThrowsValidationException()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "ram_gb": { "t": "number", "v": 16 }
            }
            """);

        ValidationException exception = Assert.Throws<
            ValidationException>(() =>
                AttributeValueBagRequestParser.Parse(
                    document.RootElement));

        Assert.Contains(
            exception.Errors,
            error => error.ErrorCode ==
                "specifications.invalid_value");
    }

    [Fact]
    public void Parse_WithMeasurement_RejectsClientCanonicalValue()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "weight": {
                "t": "measurement",
                "v": {
                  "value": 1,
                  "unit": "kg",
                  "canonicalValue": 1,
                  "canonicalUnit": "g"
                }
              }
            }
            """);

        ValidationException exception = Assert.Throws<
            ValidationException>(() =>
                AttributeValueBagRequestParser.Parse(
                    document.RootElement));

        Assert.Contains(
            exception.Errors,
            error => error.ErrorCode ==
                "specifications.measurement_requires_normalization");
    }
}