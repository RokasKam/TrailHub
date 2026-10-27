using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Gpx;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Trails.Application.Abstractions;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Trails;

/// <summary>Creates a trail from a GPX file. Name/description fall back to the file's metadata; waypoints become points of interest.</summary>
public sealed record ImportTrailFromGpxCommand(Stream Gpx, string? Name, string? Description, string Region, Difficulty Difficulty)
    : ICommand<Guid>;

internal sealed class ImportTrailFromGpxHandler(
    IGpxSerializer gpx,
    ITrailRepository trails,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock)
    : ICommandHandler<ImportTrailFromGpxCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ImportTrailFromGpxCommand command, CancellationToken cancellationToken)
    {
        var parsed = gpx.Parse(command.Gpx);
        if (parsed.IsFailure)
        {
            return parsed.Error!;
        }

        var document = parsed.Value;
        var route = new List<GeoPoint>();
        foreach (var point in document.AllPoints)
        {
            var geo = GeoPoint.Create(point.Latitude, point.Longitude, point.Elevation);
            if (geo.IsFailure)
            {
                return geo.Error!;
            }

            route.Add(geo.Value);
        }

        var created = Trail.Create(
            command.Name ?? document.Name ?? "Imported trail",
            command.Description ?? document.Description,
            command.Region,
            command.Difficulty,
            route,
            user.UserId,
            clock.GetUtcNow().UtcDateTime);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        var trail = created.Value;
        foreach (var waypoint in document.Waypoints.Take(Trail.MaxPointsOfInterest))
        {
            var location = GeoPoint.Create(waypoint.Latitude, waypoint.Longitude, waypoint.Elevation);
            if (location.IsFailure)
            {
                return location.Error!;
            }

            var type = PointOfInterest.InferType($"{waypoint.Type} {waypoint.Name}");
            var added = trail.AddPointOfInterest(waypoint.Name ?? type.ToString(), type, location.Value, waypoint.Description);
            if (added.IsFailure)
            {
                return added.Error!;
            }
        }

        trails.Add(trail);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return trail.Id;
    }
}

public sealed record UpdateTrailCommand(Guid TrailId, string Name, string? Description, string Region, Difficulty Difficulty) : ICommand;

internal sealed class UpdateTrailHandler(ITrailRepository trails, IUnitOfWork unitOfWork, ICurrentUser user)
    : ICommandHandler<UpdateTrailCommand>
{
    public async Task<Result> Handle(UpdateTrailCommand command, CancellationToken cancellationToken)
    {
        var trail = await trails.GetByIdAsync(command.TrailId, cancellationToken);
        if (trail is null)
        {
            return TrailErrors.NotFound;
        }

        var result = trail.UpdateDetails(command.Name, command.Description, command.Region, command.Difficulty, user.UserId);
        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}

public sealed record AddPointOfInterestCommand(
    Guid TrailId,
    string Name,
    PointOfInterestType Type,
    double Latitude,
    double Longitude,
    double? Elevation,
    string? Description) : ICommand<PointOfInterestDto>;

/// <summary>Any signed-in hiker can add a POI (e.g. a spring they found); only the owner can remove one.</summary>
internal sealed class AddPointOfInterestHandler(ITrailRepository trails, IUnitOfWork unitOfWork)
    : ICommandHandler<AddPointOfInterestCommand, PointOfInterestDto>
{
    public async Task<Result<PointOfInterestDto>> Handle(AddPointOfInterestCommand command, CancellationToken cancellationToken)
    {
        var location = GeoPoint.Create(command.Latitude, command.Longitude, command.Elevation);
        if (location.IsFailure)
        {
            return location.Error!;
        }

        var trail = await trails.GetByIdAsync(command.TrailId, cancellationToken);
        if (trail is null)
        {
            return TrailErrors.NotFound;
        }

        var added = trail.AddPointOfInterest(command.Name, command.Type, location.Value, command.Description);
        if (added.IsFailure)
        {
            return added.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return added.Value.ToDto();
    }
}

public sealed record RemovePointOfInterestCommand(Guid TrailId, Guid PointOfInterestId) : ICommand;

internal sealed class RemovePointOfInterestHandler(ITrailRepository trails, IUnitOfWork unitOfWork, ICurrentUser user)
    : ICommandHandler<RemovePointOfInterestCommand>
{
    public async Task<Result> Handle(RemovePointOfInterestCommand command, CancellationToken cancellationToken)
    {
        var trail = await trails.GetByIdAsync(command.TrailId, cancellationToken);
        if (trail is null)
        {
            return TrailErrors.NotFound;
        }

        var result = trail.RemovePointOfInterest(command.PointOfInterestId, user.UserId);
        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}

public sealed record AddTrailPhotoCommand(Guid TrailId, string Url, string? Caption) : ICommand;

internal sealed class AddTrailPhotoHandler(ITrailRepository trails, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<AddTrailPhotoCommand>
{
    public async Task<Result> Handle(AddTrailPhotoCommand command, CancellationToken cancellationToken)
    {
        var trail = await trails.GetByIdAsync(command.TrailId, cancellationToken);
        if (trail is null)
        {
            return TrailErrors.NotFound;
        }

        var result = trail.AddPhoto(command.Url, command.Caption, user.UserId, clock.GetUtcNow().UtcDateTime);
        if (result.IsSuccess)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}
