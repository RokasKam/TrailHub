using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Requests;

/// <summary>Points of interest along a line, e.g. the route being edited. The line is thinned out before querying.</summary>
public sealed record PoisAlongRequest(IReadOnlyList<double[]> Line, IReadOnlyCollection<PoiKind>? Kinds, double? CorridorMeters);
