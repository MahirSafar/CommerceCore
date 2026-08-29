namespace CommerceCore.Platform.Contracts;

public readonly record struct MarketId
{
    private MarketId(string code)
    {
        Code = code;
    }

    public string Code { get; }

    public static MarketId From(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Market code cannot be empty.", nameof(code));
        }

        return new MarketId(code.Trim().ToUpperInvariant());
    }

    public override string ToString() => Code;

    public static implicit operator string(MarketId id) => id.Code;
}
