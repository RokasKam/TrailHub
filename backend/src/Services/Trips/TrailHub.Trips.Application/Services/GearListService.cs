using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Trips.Application.Interfaces.Repository;
using TrailHub.Trips.Application.Interfaces.Service;
using TrailHub.Trips.Application.Mappings;
using TrailHub.Trips.Application.Requests;
using TrailHub.Trips.Application.Responses;
using TrailHub.Trips.Domain.Gear;

namespace TrailHub.Trips.Application.Services;

internal sealed class GearListService(IGearListRepository lists, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock) : IGearListService
{
    public async Task<IReadOnlyList<GearListResponse>> GetListsAsync(CancellationToken cancellationToken)
    {
        var mine = await lists.GetByOwnerAsync(user.UserId, cancellationToken);
        return mine.Select(l => l.ToResponse()).Concat(StarterLists.All.Select(s => s.ToResponse())).ToList();
    }

    public async Task<GearListResponse> CreateAsync(GearListRequest request, CancellationToken cancellationToken)
    {
        if (await lists.CountByOwnerAsync(user.UserId, cancellationToken) >= GearList.MaxListsPerUser)
        {
            throw GearErrors.TooManyLists.ToException();
        }

        var list = GearList.Create(user.UserId, request.Name, request.Items, clock.GetUtcNow().UtcDateTime).GetValueOrThrow();
        lists.Add(list);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return list.ToResponse();
    }

    public async Task<GearListResponse> UpdateAsync(Guid id, GearListRequest request, CancellationToken cancellationToken)
    {
        var list = await GetOwnAsync(id, cancellationToken);
        list.Update(request.Name, request.Items, clock.GetUtcNow().UtcDateTime).ThrowIfFailure();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return list.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var list = await GetOwnAsync(id, cancellationToken);
        lists.Remove(list);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>One of the caller's lists. Other people's lists are reported as not found.</summary>
    private async Task<GearList> GetOwnAsync(Guid id, CancellationToken cancellationToken) =>
        await lists.GetByIdAsync(id, cancellationToken) is { } list && list.OwnerId == user.UserId
            ? list
            : throw GearErrors.NotFound.ToException();
}
