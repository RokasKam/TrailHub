using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Gpx;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.Contracts;

namespace TrailHub.Activity.Application.Activities;

public sealed record RecordActivityCommand(
    Guid Id,
    string Name,
    Guid? TrailId,
    IReadOnlyList<IReadOnlyList<TrackPointDto>> Segments,
    string? Notes) : ICommand<ActivitySummaryDto>;

/// <summary>
/// Idempotent upload of a finished recording. Re-sending the same id (e.g. an offline queue
/// retrying after a timeout) returns the stored activity instead of creating a duplicate.
/// </summary>
internal sealed class RecordActivityHandler(ActivitySaver saver) : ICommandHandler<RecordActivityCommand, ActivitySummaryDto>
{
    public Task<Result<ActivitySummaryDto>> Handle(RecordActivityCommand command, CancellationToken cancellationToken)
    {
        var segments = command.Segments
            .Select(s => new ActivitySegment(s.Select(p => new TrackPoint(p.Latitude, p.Longitude, p.Elevation, DateTime.SpecifyKind(p.TimestampUtc, DateTimeKind.Utc))).ToList()))
            .ToList();
        return saver.SaveAsync(command.Id, command.Name, command.TrailId, segments, ActivitySource.Recorded, command.Notes, cancellationToken);
    }
}

public sealed record ImportActivityFromGpxCommand(Stream Gpx, string? Name, Guid? TrailId) : ICommand<ActivitySummaryDto>;

internal sealed class ImportActivityFromGpxHandler(IGpxSerializer gpx, ActivitySaver saver) : ICommandHandler<ImportActivityFromGpxCommand, ActivitySummaryDto>
{
    public async Task<Result<ActivitySummaryDto>> Handle(ImportActivityFromGpxCommand command, CancellationToken cancellationToken)
    {
        var parsed = gpx.Parse(command.Gpx);
        if (parsed.IsFailure)
        {
            return parsed.Error!;
        }

        if (parsed.Value.AllPoints.Any(p => p.TimeUtc is null))
        {
            return ActivityErrors.GpxWithoutTimestamps;
        }

        var segments = parsed.Value.Segments
            .Select(s => new ActivitySegment(s.Select(p => new TrackPoint(p.Latitude, p.Longitude, p.Elevation, p.TimeUtc!.Value)).ToList()))
            .ToList();
        return await saver.SaveAsync(
            Guid.CreateVersion7(),
            command.Name ?? parsed.Value.Name ?? "Imported hike",
            command.TrailId,
            segments,
            ActivitySource.ImportedGpx,
            parsed.Value.Description,
            cancellationToken);
    }
}

internal sealed class ActivitySaver(
    IActivityRepository activities,
    IUnitOfWork unitOfWork,
    IEventPublisher events,
    ICurrentUser user,
    TimeProvider clock)
{
    public async Task<Result<ActivitySummaryDto>> SaveAsync(
        Guid id,
        string name,
        Guid? trailId,
        IReadOnlyList<ActivitySegment> segments,
        ActivitySource source,
        string? notes,
        CancellationToken cancellationToken)
    {
        if (await activities.GetByIdAsync(id, cancellationToken) is { } existing)
        {
            return existing.UserId == user.UserId ? existing.ToSummary() : ActivityErrors.IdTaken;
        }

        var created = HikeActivity.Create(id, user.UserId, name, trailId, segments, source, notes, clock.GetUtcNow().UtcDateTime);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        var activity = created.Value;
        activities.Add(activity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await events.PublishAsync(
            new ActivityCompletedIntegrationEvent(
                activity.Id,
                activity.UserId,
                activity.TrailId,
                activity.DistanceMeters,
                activity.ElevationGainMeters,
                activity.MovingDurationSeconds,
                activity.EndedAtUtc),
            cancellationToken);

        return activity.ToSummary();
    }
}

public sealed record UpdateActivityCommand(Guid Id, string Name, string? Notes) : ICommand;

internal sealed class UpdateActivityHandler(IActivityRepository activities, IUnitOfWork unitOfWork, ICurrentUser user)
    : ICommandHandler<UpdateActivityCommand>
{
    public async Task<Result> Handle(UpdateActivityCommand command, CancellationToken cancellationToken)
    {
        var activity = await activities.GetByIdAsync(command.Id, cancellationToken);
        if (activity is null || activity.UserId != user.UserId)
        {
            return ActivityErrors.NotFound;
        }

        var result = activity.UpdateDetails(command.Name, command.Notes);
        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}

public sealed record DeleteActivityCommand(Guid Id) : ICommand;

internal sealed class DeleteActivityHandler(IActivityRepository activities, IUnitOfWork unitOfWork, IEventPublisher events, ICurrentUser user)
    : ICommandHandler<DeleteActivityCommand>
{
    public async Task<Result> Handle(DeleteActivityCommand command, CancellationToken cancellationToken)
    {
        var activity = await activities.GetByIdAsync(command.Id, cancellationToken);
        if (activity is null || activity.UserId != user.UserId)
        {
            return ActivityErrors.NotFound;
        }

        activities.Remove(activity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await events.PublishAsync(new ActivityDeletedIntegrationEvent(activity.Id, activity.UserId), cancellationToken);
        return Result.Success();
    }
}
