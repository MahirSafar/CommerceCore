namespace CommerceCore.Application.Common.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
