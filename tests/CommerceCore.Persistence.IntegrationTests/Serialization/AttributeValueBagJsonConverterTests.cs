using System.Text.Json;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Persistence.Serialization;

namespace CommerceCore.Persistence.IntegrationTests.Serialization;

public sealed class AttributeValueBagJsonConverterTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new AttributeValueBagJsonConverter() }
    };

    [Fact]
    public void Serialize_WithEquivalentBags_ProducesIdenticalOrderedJson()
    {
        AttributeValueBag first = CreateBag(
            "cpu",
            "ram_gb",
            "color",
            "available",
            "tags",
            "weight");

        AttributeValueBag second = CreateBag(
            "weight",
            "tags",
            "available",
            "color",
            "ram_gb",
            "cpu");

        string firstJson = JsonSerializer.Serialize(first, JsonOptions);
        string secondJson = JsonSerializer.Serialize(second, JsonOptions);

        Assert.Equal(firstJson, secondJson);

        using JsonDocument document = JsonDocument.Parse(firstJson);

        Assert.Equal(
            "text",
            document.RootElement
                .GetProperty("cpu")
                .GetProperty("t")
                .GetString());

        Assert.Equal(
            "integer",
            document.RootElement
                .GetProperty("ram_gb")
                .GetProperty("t")
                .GetString());

        Assert.Equal(
            "singleSelect",
            document.RootElement
                .GetProperty("color")
                .GetProperty("t")
                .GetString());
    }

    [Fact]
    public void Deserialize_AfterSerialize_RestoresEquivalentBag()
    {
        AttributeValueBag original = CreateBag(
            "cpu",
            "ram_gb",
            "color",
            "available",
            "tags",
            "weight");

        string json = JsonSerializer.Serialize(original, JsonOptions);

        AttributeValueBag restored = JsonSerializer.Deserialize<
            AttributeValueBag>(json, JsonOptions)
            ?? throw new InvalidOperationException(
                "Attribute-value bag could not be deserialized.");

        Assert.Equal(original, restored);
    }

    private static AttributeValueBag CreateBag(params string[] keys)
    {
        AttributeValueBag bag = AttributeValueBag.Empty;

        foreach (string key in keys)
        {
            bag = key switch
            {
                "cpu" => bag.With(
                    AttributeKey.Create("cpu"),
                    AttributeValue.Text.Create("Apple M4")),

                "ram_gb" => bag.With(
                    AttributeKey.Create("ram_gb"),
                    AttributeValue.Integer.Create(32)),

                "color" => bag.With(
                    AttributeKey.Create("color"),
                    AttributeValue.SingleSelect.Create("space-black")),

                "available" => bag.With(
                    AttributeKey.Create("available"),
                    AttributeValue.Boolean.Create(true)),

                "tags" => bag.With(
                    AttributeKey.Create("tags"),
                    AttributeValue.MultiSelect.Create(
                        ["blue", "red", "blue"])),

                "weight" => bag.With(
                    AttributeKey.Create("weight"),
                    AttributeValue.Measurement.Create(
                        500,
                        "g",
                        500,
                        "g")),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(keys),
                    key,
                    "Unknown test attribute key.")
            };
        }

        return bag;
    }
}