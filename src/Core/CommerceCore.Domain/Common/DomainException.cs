namespace CommerceCore.Domain.Common;

public abstract class DomainException(string message) : Exception(message)
{
}
