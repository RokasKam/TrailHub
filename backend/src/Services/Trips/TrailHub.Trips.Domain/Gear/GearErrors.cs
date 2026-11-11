using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Domain.Gear;

public static class GearErrors
{
    public static readonly Error NotFound = Error.NotFound("Gear.NotFound", "The gear list does not exist.");
    public static readonly Error InvalidName = Error.Validation("Gear.InvalidName", $"Name is required and must be at most {GearList.MaxNameLength} characters.");
    public static readonly Error ItemTooLong = Error.Validation("Gear.ItemTooLong", $"Items must be at most {Trip.MaxNameLength} characters.");
    public static readonly Error TooManyItems = Error.Validation("Gear.TooManyItems", $"A gear list can have at most {GearList.MaxItems} items.");
    public static readonly Error TooManyLists = Error.Validation("Gear.TooManyLists", $"You can have at most {GearList.MaxListsPerUser} gear lists.");
}
