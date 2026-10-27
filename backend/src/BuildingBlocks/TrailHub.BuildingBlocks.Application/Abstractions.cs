namespace TrailHub.BuildingBlocks.Application;

/// <summary>The authenticated caller. <see cref="UserId"/> is the Auth0 subject (<c>sub</c>) claim.</summary>
public interface ICurrentUser
{
    string UserId { get; }

    string? Email { get; }

    string? Name { get; }
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Publishes integration events to other services through the message bus.</summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : class;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record PageRequest
{
    public const int MaxPageSize = 100;

    public PageRequest(int page = 1, int pageSize = 20)
    {
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, MaxPageSize);
    }

    public int Page { get; }

    public int PageSize { get; }

    public int Skip => (Page - 1) * PageSize;
}
