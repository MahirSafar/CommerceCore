using CommerceCore.Application.Common.Abstractions;

namespace CommerceCore.Infrastructure.Common.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

