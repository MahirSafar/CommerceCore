using System.Text.Json;
using CommerceCore.Api.Endpoints.V1.Products;
using CommerceCore.Application.Catalog.Products.Commands.SetProductSpecifications;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using FluentValidation;

namespace CommerceCore.Api.UnitTests;

public sealed class AttributeValueBagRequestParserTests
{
    [Fact]
    public void Parse_WithValidTaggedValues_CreatesTypedInput()
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

        ProductSpecificationsInput result =
            AttributeValueBagRequestParser.Parse(document.RootElement);

        Assert.Equal(6, result.Count);

        Assert.Equal(
            "Gaming laptop",
            Assert.IsType<AttributeValue.Text>(
                GetTypedValue(result, "title")).Value);

        Assert.Equal(
            16L,
            Assert.IsType<AttributeValue.Integer>(
                GetTypedValue(result, "ram_gb")).Value);

        Assert.Equal(
            15.6m,
            Assert.IsType<AttributeValue.Decimal>(
                GetTypedValue(result, "screen_size")).Value);

        Assert.True(
            Assert.IsType<AttributeValue.Boolean>(
                GetTypedValue(result, "touch")).Value);

        Assert.Equal(
            "space-black",
            Assert.IsType<AttributeValue.SingleSelect>(
                GetTypedValue(result, "finish")).OptionCode);

        AttributeValue.MultiSelect ports = Assert.IsType<
            AttributeValue.MultiSelect>(
            GetTypedValue(result, "ports"));

        Assert.Equal(["hdmi", "usb-c"], ports.OptionCodes);
    }

    [Fact]
    public void Parse_WithRawMeasurement_CreatesMeasurementInput()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "weight": {
                "t": "measurement",
                "v": {
                  "value": 1.5,
                  "unit": "kg"
                }
              }
            }
            """);

        ProductSpecificationsInput result =
            AttributeValueBagRequestParser.Parse(document.RootElement);

        ProductSpecificationInputValue.Measurement measurement =
            Assert.IsType<ProductSpecificationInputValue.Measurement>(
                result.Values[AttributeKey.Create("weight")]);

        Assert.Equal(1.5m, measurement.Value);
        Assert.Equal("kg", measurement.Unit);
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
                AttributeValueBagRequestParser.Parse(document.RootElement));

        Assert.Contains(
            exception.Errors,
            error => error.ErrorCode ==
                "specifications.invalid_value");
    }

    [Fact]
    public void Parse_WithMeasurementCanonicalFields_ThrowsValidationException()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "weight": {
                "t": "measurement",
                "v": {
                  "value": 1,
                  "unit": "kg",
                  "canonicalValue": 1000,
                  "canonicalUnit": "g"
                }
              }
            }
            """);

        ValidationException exception = Assert.Throws<
            ValidationException>(() =>
                AttributeValueBagRequestParser.Parse(document.RootElement));

        Assert.Contains(
            exception.Errors,
            error => error.ErrorCode ==
                "specifications.invalid_value");
    }

    private static AttributeValue GetTypedValue(
        ProductSpecificationsInput input,
        string key)
    {
        ProductSpecificationInputValue.Typed typed = Assert.IsType<
            ProductSpecificationInputValue.Typed>(
            input.Values[AttributeKey.Create(key)]);

        return typed.Value;
    }
}