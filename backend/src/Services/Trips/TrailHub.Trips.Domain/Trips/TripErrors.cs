using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Trips.Domain.Trips;

public static class TripErrors
{
    public static readonly Error NotFound = Error.NotFound("Trip.NotFound", "The trip does not exist.");
    public static readonly Error InvalidName = Error.Validation("Trip.InvalidName", $"Name is required and must be at most {Trip.MaxNameLength} characters.");
    public static readonly Error TextTooLong = Error.Validation("Trip.TextTooLong", "Text is too long.");
    public static readonly Error EndBeforeStart = Error.Validation("Trip.EndBeforeStart", "The trip cannot end before it starts.");
    public static readonly Error TooLong = Error.Validation("Trip.TooLong", $"A trip can be at most {Trip.MaxDays} days.");
    public static readonly Error NotEditable = Error.Conflict("Trip.NotEditable", "Completed or cancelled trips can't be changed.");
    public static readonly Error DayNotFound = Error.NotFound("Trip.DayNotFound", "The trip has no such day.");
    public static readonly Error TrailAlreadyOnDay = Error.Conflict("Trip.TrailAlreadyOnDay", "That trail is already planned for this day.");
    public static readonly Error TrailNotOnDay = Error.NotFound("Trip.TrailNotOnDay", "That trail isn't planned for this day.");
    public static readonly Error TooManyTrails = Error.Validation("Trip.TooManyTrails", $"A day can have at most {TripDay.MaxTrailsPerDay} trails.");
    public static readonly Error InvalidChecklistItem = Error.Validation("Trip.InvalidChecklistItem", "Checklist item text is required.");
    public static readonly Error ChecklistFull = Error.Validation("Trip.ChecklistFull", $"A checklist can have at most {Trip.MaxChecklistItems} items.");
    public static readonly Error ChecklistItemNotFound = Error.NotFound("Trip.ChecklistItemNotFound", "The checklist item does not exist.");
    public static readonly Error SharedItemNeedsGroup = Error.Validation("Trip.SharedItemNeedsGroup", "Share the trip with a group before adding group gear.");
    public static readonly Error NotASharedItem = Error.Validation("Trip.NotASharedItem", "Only group gear can be claimed.");
    public static readonly Error ClaimedBySomeoneElse = Error.Conflict("Trip.ClaimedBySomeoneElse", "Someone else is already bringing this.");
    public static readonly Error NotYourItem = Error.Forbidden("Trip.NotYourItem", "Only the trip owner or the person who added it can remove this item.");
    public static readonly Error NotOwner = Error.Forbidden("Trip.NotOwner", "Only the trip owner can change the plan.");
    public static readonly Error GroupNotFound = Error.NotFound("Trip.GroupNotFound", "That group doesn't exist or you're not in it.");
}
