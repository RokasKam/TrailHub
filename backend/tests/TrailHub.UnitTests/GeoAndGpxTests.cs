using System.Text;
using TrailHub.BuildingBlocks.Application.Gpx;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.BuildingBlocks.Infrastructure.Gpx;

namespace TrailHub.UnitTests;

public sealed class RouteMetricsTests
{
    [Fact]
    public void One_degree_of_latitude_is_about_111_km()
    {
        var distance = RouteMetrics.HaversineMeters(new GeoPoint(45, 10), new GeoPoint(46, 10));

        Assert.InRange(distance, 111_100, 111_300);
    }

    [Fact]
    public void Elevation_jitter_below_threshold_is_not_counted_as_climbing()
    {
        var flatButNoisy = Enumerable.Range(0, 100)
            .Select(i => new GeoPoint(45 + i * 0.0001, 10, 500 + (i % 2 == 0 ? 0 : 2)))
            .ToList();

        var metrics = RouteMetrics.Calculate(flatButNoisy);

        Assert.Equal(0, metrics.ElevationGainMeters);
        Assert.Equal(0, metrics.ElevationLossMeters);
    }

    [Fact]
    public void Gain_and_loss_follow_the_profile()
    {
        var route = new[] { 100.0, 150, 120, 300, 250 }.Select((e, i) => new GeoPoint(45 + i * 0.001, 10, e)).ToList();

        var metrics = RouteMetrics.Calculate(route);

        Assert.Equal(50 + 180, metrics.ElevationGainMeters);
        Assert.Equal(30 + 50, metrics.ElevationLossMeters);
        Assert.Equal(300, metrics.MaxElevationMeters);
        Assert.Equal(100, metrics.MinElevationMeters);
    }

    [Fact]
    public void Elevation_profile_is_downsampled_but_keeps_both_ends()
    {
        var route = Enumerable.Range(0, 1000).Select(i => new GeoPoint(45 + i * 0.0001, 10, i)).ToList();

        var profile = ElevationProfile.Build(route, maxSamples: 50);

        Assert.Equal(50, profile.Count);
        Assert.Equal(0, profile[0].ElevationMeters);
        Assert.Equal(999, profile[^1].ElevationMeters);
        Assert.Equal(0, profile[0].DistanceMeters);
    }

    [Fact]
    public void Elevation_profile_does_not_count_the_gap_between_paused_segments()
    {
        // Two 1-degree walks with a 10-degree jump between them while paused.
        IReadOnlyList<GeoPoint> first = [new(45, 10, 100), new(46, 10, 200)];
        IReadOnlyList<GeoPoint> second = [new(56, 10, 300), new(57, 10, 400)];

        var profile = ElevationProfile.Build([first, second]);

        Assert.Equal(4, profile.Count);
        Assert.Equal(profile[1].DistanceMeters, profile[2].DistanceMeters);
        Assert.InRange(profile[^1].DistanceMeters, 222_200, 222_600);
    }
}

public sealed class GpxSerializerTests
{
    private const string Sample = """
        <?xml version="1.0" encoding="UTF-8"?>
        <gpx version="1.1" creator="test" xmlns="http://www.topografix.com/GPX/1/1">
          <metadata><name>Ridge Loop</name><desc>Nice views</desc></metadata>
          <wpt lat="45.001" lon="10.001"><ele>620</ele><name>Spring</name><type>Drinking Water</type></wpt>
          <trk>
            <trkseg>
              <trkpt lat="45.0" lon="10.0"><ele>600</ele><time>2026-06-01T08:00:00Z</time></trkpt>
              <trkpt lat="45.001" lon="10.0"><ele>610</ele><time>2026-06-01T08:01:00Z</time></trkpt>
            </trkseg>
            <trkseg>
              <trkpt lat="45.002" lon="10.0"><ele>630</ele><time>2026-06-01T08:30:00Z</time></trkpt>
            </trkseg>
          </trk>
        </gpx>
        """;

    private readonly GpxSerializer _gpx = new();

    [Fact]
    public void Parses_metadata_segments_and_waypoints()
    {
        var doc = Parse(Sample);

        Assert.Equal("Ridge Loop", doc.Name);
        Assert.Equal("Nice views", doc.Description);
        Assert.Equal(2, doc.Segments.Count);
        Assert.Equal(3, doc.AllPoints.Count());
        Assert.Equal(new DateTime(2026, 6, 1, 8, 1, 0, DateTimeKind.Utc), doc.Segments[0][1].TimeUtc);
        var spring = Assert.Single(doc.Waypoints);
        Assert.Equal("Drinking Water", spring.Type);
    }

    [Fact]
    public void Round_trips_through_write_and_parse()
    {
        var original = Parse(Sample);

        var reparsed = Parse(_gpx.Write(original));

        Assert.Equal(original.Name, reparsed.Name);
        Assert.Equal(original.Segments.Count, reparsed.Segments.Count);
        Assert.Equal(original.AllPoints, reparsed.AllPoints);
        Assert.Equal(original.Waypoints.Count, reparsed.Waypoints.Count);
    }

    [Fact]
    public void Reads_routes_and_gpx_1_0_namespace()
    {
        var doc = Parse("""<gpx version="1.0" xmlns="http://www.topografix.com/GPX/1/0"><rte><rtept lat="1" lon="2"/><rtept lat="1.1" lon="2"/></rte></gpx>""");

        Assert.Equal(2, Assert.Single(doc.Segments).Count);
    }

    [Fact]
    public void Rejects_dtds_to_prevent_xxe()
    {
        const string xxe = """<?xml version="1.0"?><!DOCTYPE gpx [<!ENTITY x SYSTEM "file:///etc/passwd">]><gpx><wpt lat="1" lon="1"><name>&x;</name></wpt></gpx>""";

        var result = _gpx.Parse(new MemoryStream(Encoding.UTF8.GetBytes(xxe)));

        Assert.True(result.IsFailure);
        Assert.Equal("Gpx.Malformed", result.Error!.Code);
    }

    [Fact]
    public void Rejects_points_without_coordinates()
    {
        var result = _gpx.Parse(new MemoryStream(Encoding.UTF8.GetBytes("""<gpx><trk><trkseg><trkpt lat="1"/></trkseg></trk></gpx>""")));

        Assert.Equal("Gpx.InvalidPoint", result.Error!.Code);
    }

    private GpxDocument Parse(string xml) => _gpx.Parse(new MemoryStream(Encoding.UTF8.GetBytes(xml))).Value;
}
