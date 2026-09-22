namespace CommerceCore.Platform.Identity;

public sealed class PublicStorefrontReadMetadata
{
    public static PublicStorefrontReadMetadata Instance { get; } = new();

    private PublicStorefrontReadMetadata()
    {
    }
}
