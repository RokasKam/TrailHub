using TrailHub.Trips.Application.Interfaces.Service;
using TrailHub.Trips.Application.Requests;
using TrailHub.Trips.Domain.Gear;

namespace TrailHub.Trips.Application.Services;

internal sealed class GearSuggestionService(TripEditor editor) : IGearSuggestionService
{
    public async Task<IReadOnlyList<GearSuggestion>> GetSuggestionsAsync(Guid tripId, GearSuggestionsRequest request, CancellationToken cancellationToken)
    {
        var (trip, _) = await editor.LoadVisibleAsync(tripId, cancellationToken);

        // Without route facts, read the kind of night from what the planner typed ("Refuge du Lac Blanc", "Camping …").
        var guessed = trip.Days.Where(d => d.DayNumber < trip.DayCount && d.Overnight is not null).Select(d => OvernightKind(d.Overnight!)).ToList();
        var facts = new TripGearFacts(
            trip.DayCount,
            request.TentNights ?? guessed.Count(k => k == "tent"),
            request.HutNights ?? guessed.Count(k => k == "hut"),
            request.HotelNights ?? guessed.Count(k => k == "hotel"),
            request.MaxElevationMeters,
            request.LongestDayMinutes,
            trip.GroupId is not null);
        return GearSuggestions.For(facts, trip.Checklist.Select(i => i.Text)).ToList();
    }

    internal static string? OvernightKind(string overnight)
    {
        var o = overnight.ToLowerInvariant();
        string[] huts = ["hut", "hütte", "huette", "refuge", "rifugio", "cabane", "chata", "schronisko", "bivouac", "bivacco", "shelter"];
        string[] tents = ["camp", "tent", "bivy"];
        string[] hotels = ["hotel", "hostel", "guest", "inn", "auberge", "gîte", "gite", "pension", "b&b", "chalet"];
        return huts.Any(o.Contains) ? "hut" : tents.Any(o.Contains) ? "tent" : hotels.Any(o.Contains) ? "hotel" : null;
    }
}
