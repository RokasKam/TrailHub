using TrailHub.Trails.Domain.Community;

namespace TrailHub.Trails.Application.Interfaces.Repository;

public interface IConditionReportRepository
{
    void Add(ConditionReport report);

    Task<IReadOnlyList<ConditionReport>> GetRecentAsync(Guid trailId, int take, CancellationToken cancellationToken);
}
