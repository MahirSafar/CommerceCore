namespace CommerceCore.Domain.Common;

public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }
    public static Money Create(decimal amount, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");

        var trimmedCurrency = currency.Trim().ToUpperInvariant();

        return trimmedCurrency.Length != 3 || !trimmedCurrency.All(char.IsAsciiLetter)
            ? throw new ArgumentException("Valyuta 3 hərfli ISO kodu olmalıdır.", nameof(currency))
            : new Money(amount, trimmedCurrency);
    }
    public override string ToString() => $"{Amount} {Currency}";
}
