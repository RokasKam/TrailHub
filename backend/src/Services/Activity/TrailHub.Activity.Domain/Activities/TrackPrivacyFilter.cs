using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Activity.Domain.Activities;

/// <summary>Applies a hike's <see cref="TrackPrivacy"/> to its track before group members see it.</summary>
public static class TrackPrivacyFilter
{
    /// <summary>Points closer than this to the start or the finish are hidden with <see cref="TrackPrivacy.HideEnds"/>.</summary>
    public const double HiddenEndRadiusMeters = 200;

    /// <summary>
    /// The segments group members may see. With <see cref="TrackPrivacy.HideEnds"/> every point within
    /// <see cref="HiddenEndRadiusMeters"/> of the first or last point is dropped, wherever it is on the track (a loop
    /// that passes home again stays hidden there too), and the track is split where points were dropped so no line
    /// is drawn across the hidden area. Pieces left with fewer than two points are dropped.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<GeoPoint>> Apply(IReadOnlyList<IReadOnlyList<GeoPoint>> segments, TrackPrivacy privacy)
    {
        switch (privacy)
        {
            case TrackPrivacy.Full:
                return segments;
            case TrackPrivacy.HideEnds:
                break;
            default:
                return [];
        }

        var nonEmpty = segments.Where(s => s.Count > 0).ToList();
        if (nonEmpty.Count == 0)
        {
            return [];
        }

        var start = nonEmpty[0][0];
        var finish = nonEmpty[^1][^1];
        var pieces = new List<IReadOnlyList<GeoPoint>>();
        foreach (var segment in nonEmpty)
        {
            var piece = new List<GeoPoint>();
            foreach (var point in segment)
            {
                if (RouteMetrics.HaversineMeters(point, start) < HiddenEndRadiusMeters || RouteMetrics.HaversineMeters(point, finish) < HiddenEndRadiusMeters)
                {
                    Close(piece, pieces);
                    piece = [];
                }
                else
                {
                    piece.Add(point);
                }
            }

            Close(piece, pieces);
        }

        return pieces;
    }

    private static void Close(List<GeoPoint> piece, List<IReadOnlyList<GeoPoint>> pieces)
    {
        if (piece.Count >= 2)
        {
            pieces.Add(piece);
        }
    }
}
