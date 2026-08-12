namespace CommerceCore.Domain.Common.Interfaces;

public interface IAuditableEntity
{
    DateTimeOffset CreatedAtUtc { get; }
    string? CreatedBy { get; }

    DateTimeOffset? UpdatedAtUtc { get; }
    string? UpdatedBy { get; }
}
