using TrailHub.Trips.Application.Models;
using TrailHub.Trips.Application.Responses;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Application.Mappings;

internal static class TripMappings
{
    public static TripResponse ToResponse(this Trip t, string viewerId, GroupInfo? group, ExpenseSummaryResponse expenseSummary)
    {
        var personal = t.Checklist.Where(i => i.Kind == ChecklistItemKind.Personal).ToList();
        return new TripResponse(
            t.Id,
            t.Name,
            t.Description,
            t.StartDate,
            t.EndDate,
            t.DayCount,
            t.Status,
            t.Days.OrderBy(d => d.DayNumber).Select(d => new TripDayResponse(d.DayNumber, t.StartDate.AddDays(d.DayNumber - 1), d.Notes, d.Overnight, d.TrailIds.ToList())).ToList(),
            t.Checklist
                .Select(i => new ChecklistItemResponse(
                    i.Id,
                    i.Text,
                    i.Kind,
                    i.IsDoneFor(viewerId),
                    i.ClaimedBy,
                    i.ClaimedBy is { } claimer ? group?.NameOf(claimer) ?? "Former member" : null,
                    i.AddedBy,
                    t.CanRemoveChecklistItem(viewerId, i)))
                .ToList(),
            t.CreatedAtUtc,
            t.OwnerId,
            t.OwnerId == viewerId,
            t.GroupId,
            group?.Name,
            group is null
                ? []
                : group.Members
                    .Select(m => new TripMemberResponse(m.UserId, m.DisplayName, m.UserId == t.OwnerId, personal.Count(i => i.DoneBy.Contains(m.UserId))))
                    .OrderByDescending(m => m.IsOwner)
                    .ThenBy(m => m.DisplayName)
                    .ToList(),
            t.RouteId,
            t.Currency,
            expenseSummary);
    }

    public static TripSummaryResponse ToSummaryResponse(this Trip t, string viewerId, GroupInfo? group) => new(
        t.Id,
        t.Name,
        t.StartDate,
        t.EndDate,
        t.DayCount,
        t.Status,
        t.Days.Sum(d => d.TrailIds.Count),
        t.GroupId,
        group?.Name,
        t.OwnerId == viewerId,
        t.GroupId is null ? 0 : t.Checklist.Count(i => i.Kind == ChecklistItemKind.Shared && i.ClaimedBy is null),
        t.Currency);
}
