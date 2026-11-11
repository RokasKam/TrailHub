using TrailHub.Activity.Application.Responses;
using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Application;

namespace TrailHub.Activity.Application.Interfaces.Repository;

public interface IActivityRepository
{
    Task<HikeActivity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(HikeActivity activity);

    void Remove(HikeActivity activity);

    Task<PagedResult<ActivitySummaryResponse>> GetPageAsync(string userId, PageRequest page, CancellationToken cancellationToken);

    Task<ActivityTotalsResponse> GetTotalsAsync(string userId, DateTime? sinceUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<MonthlyStatisticsResponse>> GetMonthlyAsync(string userId, DateTime sinceUtc, CancellationToken cancellationToken);
}
