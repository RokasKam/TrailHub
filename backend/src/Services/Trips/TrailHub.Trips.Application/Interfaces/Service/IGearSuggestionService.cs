using TrailHub.Trips.Application.Requests;
using TrailHub.Trips.Domain.Gear;

namespace TrailHub.Trips.Application.Interfaces.Service;

/// <summary>Rule-based packing suggestions for a trip.</summary>
public interface IGearSuggestionService
{
    /// <summary>Suggestions for a trip the caller can see. Route facts missing from the request are guessed from the days' overnight names.</summary>
    Task<IReadOnlyList<GearSuggestion>> GetSuggestionsAsync(Guid tripId, GearSuggestionsRequest request, CancellationToken cancellationToken);
}
