namespace CommerceCore.Domain.Common.ValueObjects;

public sealed record Money
{
    public const int MaximumScale = 4;

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Create(decimal amount, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Amount cannot be negative.");
        }

        if (GetScale(amount) > MaximumScale)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                $"Amount cannot have more than {MaximumScale} decimal places.");
        }

        var normalizedCurrency = currency.Trim().ToUpperInvariant();

        if (normalizedCurrency.Length != 3 ||
            !normalizedCurrency.All(character =>
                character is >= 'A' and <= 'Z'))
        {
            throw new ArgumentException(
                "Currency must be a three-letter ISO 4217 code.",
                nameof(currency));
        }

        return new Money(amount, normalizedCurrency);
    }

    public override string ToString() => $"{Amount} {Currency}";

    private static int GetScale(decimal value)
        => (decimal.GetBits(value)[3] >> 16) & 0xFF;
}