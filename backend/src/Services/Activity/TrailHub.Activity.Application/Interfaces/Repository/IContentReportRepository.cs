using TrailHub.Activity.Domain.Feed;

namespace TrailHub.Activity.Application.Interfaces.Repository;

public interface IContentReportRepository
{
    Task<ContentReport?> GetByUserAsync(Guid contentId, string userId, CancellationToken cancellationToken);

    Task<int> CountOpenAsync(Guid contentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContentReport>> GetOpenAsync(Guid contentId, CancellationToken cancellationToken);

    /// <summary>Every open report, for the moderators' queue.</summary>
    Task<IReadOnlyList<ContentReport>> GetAllOpenAsync(CancellationToken cancellationToken);

    /// <summary>All of an item's reports, open or resolved (they're deleted with the item).</summary>
    Task<IReadOnlyList<ContentReport>> GetAllAsync(Guid contentId, CancellationToken cancellationToken);

    void Add(ContentReport report);

    void Remove(ContentReport report);
}
