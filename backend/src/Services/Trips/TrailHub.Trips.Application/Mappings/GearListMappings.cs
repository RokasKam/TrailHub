using TrailHub.Trips.Application.Responses;
using TrailHub.Trips.Domain.Gear;

namespace TrailHub.Trips.Application.Mappings;

internal static class GearListMappings
{
    public const string StarterPrefix = "starter:";

    public static GearListResponse ToResponse(this GearList l) => new(l.Id.ToString(), l.Name, l.Items, false, l.UpdatedAtUtc);

    public static GearListResponse ToResponse(this StarterList s) => new(StarterPrefix + s.Id, s.Name, s.Items, true, null);
}
