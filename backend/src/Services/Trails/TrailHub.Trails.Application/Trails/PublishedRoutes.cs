using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Trails.Application.Abstractions;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Trails;

/// <summary>
/// A planned route its author published from the Routes service. Creates a public trail with the route's id, or,
/// when the route is published again, updates that trail's details and line (reviews, photos and favourites stay).
/// </summary>
public sealed record PublishRouteAsTrailCommand(
    Guid RouteId,
    string OwnerId,
    string Name,
    string? Description,
    string Region,
    Difficulty Difficulty,
    IReadOnlyList<double?[]> Points,
    DateTime PublishedAtUtc) : ICommand;

internal sealed class PublishRouteAsTrailHandler(ITrailRepository trails, IUnitOfWork unitOfWork) : ICommandHandler<PublishRouteAsTrailCommand>
{
    public async Task<Result> Handle(PublishRouteAsTrailCommand command, CancellationToken cancellationToken)
    {
        var route = new List<GeoPoint>(command.Points.Count);
        foreach (var p in command.Points)
        {
            if (p.Length < 2 || p[0] is not { } lat || p[1] is not { } lon || GeoPoint.Create(lat, lon, p.Length > 2 ? p[2] : null) is not { IsSuccess: true } point)
            {
                return TrailErrors.RouteTooShort;
            }

            route.Add(point.Value);
        }

        if (await trails.GetByIdAsync(command.RouteId, cancellationToken) is { } existing)
        {
            var updated = existing.UpdateDetails(command.Name, command.Description, command.Region, command.Difficulty, command.OwnerId);
            if (updated.IsFailure)
            {
                return updated;
            }

            var replaced = existing.ReplaceRoute(route, command.OwnerId);
            if (replaced.IsFailure)
            {
                return replaced;
            }

            // Publishing again undoes the author's own unpublish; a trail hidden after reports stays hidden.
            existing.Unhide(HiddenBy.Author);
        }
        else
        {
            var created = Trail.Create(command.Name, command.Description, command.Region, command.Difficulty, route, command.OwnerId, command.PublishedAtUtc, command.RouteId);
            if (created.IsFailure)
            {
                return created.Error!;
            }

            trails.Add(created.Value);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
