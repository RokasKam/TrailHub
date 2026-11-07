using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trails.Application.Abstractions;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Community;

/// <summary>Who may review reports and hide or restore any trail (configured, not stored per user).</summary>
public interface IModerators
{
    bool IsModerator(string userId);
}

public interface ITrailReportRepository
{
    Task<TrailReport?> GetByUserAsync(Guid trailId, string userId, CancellationToken cancellationToken);

    Task<int> CountOpenAsync(Guid trailId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TrailReport>> GetOpenAsync(Guid trailId, CancellationToken cancellationToken);

    /// <summary>Trails with open reports or hidden by reports or moderators, most reported first.</summary>
    Task<IReadOnlyList<ModerationItemDto>> GetQueueAsync(CancellationToken cancellationToken);

    void Add(TrailReport report);
}

public sealed record ModerationReportDto(ReportReason Reason, string? Details, DateTime CreatedAtUtc);

/// <param name="HiddenBy">Null while the trail is still in Discover.</param>
public sealed record ModerationItemDto(Guid TrailId, string Name, string Region, string CreatedBy, HiddenBy? HiddenBy, int OpenReports, IReadOnlyList<ModerationReportDto> Reports);

public sealed record ReportTrailCommand(Guid TrailId, ReportReason Reason, string? Details) : ICommand;

/// <summary>Files (or updates) the caller's report. The third distinct open report takes the trail out of Discover.</summary>
internal sealed class ReportTrailHandler(ITrailRepository trails, ITrailReportRepository reports, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<ReportTrailCommand>
{
    public async Task<Result> Handle(ReportTrailCommand command, CancellationToken cancellationToken)
    {
        var trail = await trails.GetByIdAsync(command.TrailId, cancellationToken);
        if (trail is null)
        {
            return TrailErrors.NotFound;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (await reports.GetByUserAsync(trail.Id, user.UserId, cancellationToken) is { } existing)
        {
            if (existing.Update(command.Reason, command.Details, now) is { IsFailure: true } failed)
            {
                return failed;
            }
        }
        else
        {
            var created = TrailReport.Create(trail, user.UserId, command.Reason, command.Details, now);
            if (created.IsFailure)
            {
                return created.Error!;
            }

            reports.Add(created.Value);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (await reports.CountOpenAsync(trail.Id, cancellationToken) >= Trail.AutoHideReportCount && !trail.IsHidden)
        {
            trail.Hide(HiddenBy.Reports, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}

public sealed record GetModerationQueueQuery : IQuery<IReadOnlyList<ModerationItemDto>>;

internal sealed class GetModerationQueueHandler(ITrailReportRepository reports, IModerators moderators, ICurrentUser user)
    : IQueryHandler<GetModerationQueueQuery, IReadOnlyList<ModerationItemDto>>
{
    public async Task<Result<IReadOnlyList<ModerationItemDto>>> Handle(GetModerationQueueQuery query, CancellationToken cancellationToken) =>
        moderators.IsModerator(user.UserId) ? Result<IReadOnlyList<ModerationItemDto>>.Success(await reports.GetQueueAsync(cancellationToken)) : TrailErrors.NotModerator;
}

/// <summary>A moderator hides a trail or puts it back in Discover. Either way, its open reports are resolved.</summary>
public sealed record ModerateTrailCommand(Guid TrailId, bool Hidden) : ICommand;

internal sealed class ModerateTrailHandler(ITrailRepository trails, ITrailReportRepository reports, IModerators moderators, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<ModerateTrailCommand>
{
    public async Task<Result> Handle(ModerateTrailCommand command, CancellationToken cancellationToken)
    {
        if (!moderators.IsModerator(user.UserId))
        {
            return TrailErrors.NotModerator;
        }

        var trail = await trails.GetByIdAsync(command.TrailId, cancellationToken);
        if (trail is null)
        {
            return TrailErrors.NotFound;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (command.Hidden)
        {
            trail.Hide(HiddenBy.Moderator, now);
        }
        else
        {
            trail.Unhide(HiddenBy.Moderator);
        }

        foreach (var report in await reports.GetOpenAsync(trail.Id, cancellationToken))
        {
            report.Resolve(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>The author took the route the trail came from out of Discover (Routes service). Unknown trails are ignored.</summary>
public sealed record UnpublishTrailCommand(Guid TrailId, string OwnerId, DateTime UnpublishedAtUtc) : ICommand;

internal sealed class UnpublishTrailHandler(ITrailRepository trails, IUnitOfWork unitOfWork) : ICommandHandler<UnpublishTrailCommand>
{
    public async Task<Result> Handle(UnpublishTrailCommand command, CancellationToken cancellationToken)
    {
        var trail = await trails.GetByIdAsync(command.TrailId, cancellationToken);
        if (trail is null || trail.CreatedBy != command.OwnerId)
        {
            return Result.Success();
        }

        trail.Hide(HiddenBy.Author, command.UnpublishedAtUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal static class TrailVisibility
{
    /// <summary>Hidden trails are visible only to their author and moderators; to everyone else they don't exist.</summary>
    public static bool CanSee(this Trail trail, string userId, IModerators moderators) =>
        !trail.IsHidden || trail.CreatedBy == userId || moderators.IsModerator(userId);
}
