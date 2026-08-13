namespace CommerceCore.Application.Common.Abstractions;

public interface ICurrentUser
{
    string? UserId { get; }
}
