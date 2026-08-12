namespace CommerceCore.Domain.Common.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string code, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}