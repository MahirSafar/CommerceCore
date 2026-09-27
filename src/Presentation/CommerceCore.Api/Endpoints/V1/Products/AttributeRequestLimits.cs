using System.Text.Json;

namespace CommerceCore.Api.Endpoints.V1.Products;

internal static class AttributeRequestLimits
{
    public const int MaximumAttributes = 50;

    public static bool HasTooManyAttributes(JsonElement value)
    {
        using JsonElement.ObjectEnumerator properties =
            value.EnumerateObject();

        for (int count = 0; count <= MaximumAttributes; count++)
        {
            if (!properties.MoveNext())
            {
                return false;
            }
        }

        return true;
    }
}
