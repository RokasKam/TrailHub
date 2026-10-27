using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using TrailHub.BuildingBlocks.Application.Gpx;
using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.BuildingBlocks.Infrastructure.Gpx;

/// <summary>Reads GPX 1.0/1.1 (tracks, routes, waypoints) and writes GPX 1.1.</summary>
public sealed class GpxSerializer : IGpxSerializer
{
    private const long MaxDocumentCharacters = 50 * 1024 * 1024;
    private static readonly XNamespace Ns = "http://www.topografix.com/GPX/1/1";

    public Result<GpxDocument> Parse(Stream gpx)
    {
        XDocument xml;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxDocumentCharacters,
            };
            using var reader = XmlReader.Create(gpx, settings);
            xml = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            return Error.Validation("Gpx.Malformed", $"The file is not valid GPX: {ex.Message}");
        }

        var root = xml.Root;
        if (root?.Name.LocalName != "gpx")
        {
            return Error.Validation("Gpx.Malformed", "The file has no <gpx> root element.");
        }

        try
        {
            var segments = new List<IReadOnlyList<GpxTrackPoint>>();
            foreach (var segment in Elements(root, "trk").SelectMany(t => Elements(t, "trkseg")))
            {
                AddSegment(segments, Elements(segment, "trkpt"));
            }

            foreach (var route in Elements(root, "rte"))
            {
                AddSegment(segments, Elements(route, "rtept"));
            }

            var waypoints = Elements(root, "wpt")
                .Select(w => new GpxWaypoint(
                    Child(w, "name"),
                    Child(w, "type") ?? Child(w, "sym"),
                    Child(w, "desc"),
                    Coordinate(w, "lat"),
                    Coordinate(w, "lon"),
                    Number(Child(w, "ele"))))
                .ToList();

            var firstTrack = Elements(root, "trk").Concat(Elements(root, "rte")).FirstOrDefault();
            var metadata = Elements(root, "metadata").FirstOrDefault();
            var name = (metadata is null ? null : Child(metadata, "name")) ?? (firstTrack is null ? null : Child(firstTrack, "name"));
            var description = (metadata is null ? null : Child(metadata, "desc")) ?? (firstTrack is null ? null : Child(firstTrack, "desc"));

            return new GpxDocument(name, description, segments, waypoints);
        }
        catch (FormatException ex)
        {
            return Error.Validation("Gpx.InvalidPoint", ex.Message);
        }
    }

    public string Write(GpxDocument document)
    {
        var trk = new XElement(Ns + "trk");
        if (document.Name is not null)
        {
            trk.Add(new XElement(Ns + "name", document.Name));
        }

        foreach (var segment in document.Segments)
        {
            trk.Add(new XElement(Ns + "trkseg", segment.Select(p => PointElement("trkpt", p.Latitude, p.Longitude, p.Elevation, p.TimeUtc))));
        }

        var gpx = new XElement(
            Ns + "gpx",
            new XAttribute("version", "1.1"),
            new XAttribute("creator", "TrailHub"),
            new XElement(
                Ns + "metadata",
                document.Name is null ? null : new XElement(Ns + "name", document.Name),
                document.Description is null ? null : new XElement(Ns + "desc", document.Description)),
            document.Waypoints.Select(w =>
            {
                var element = PointElement("wpt", w.Latitude, w.Longitude, w.Elevation, null);
                element.Add(
                    w.Name is null ? null : new XElement(Ns + "name", w.Name),
                    w.Description is null ? null : new XElement(Ns + "desc", w.Description),
                    w.Type is null ? null : new XElement(Ns + "type", w.Type));
                return element;
            }),
            trk);

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), gpx).Declaration + Environment.NewLine + gpx;
    }

    private static void AddSegment(List<IReadOnlyList<GpxTrackPoint>> segments, IEnumerable<XElement> points)
    {
        var parsed = points
            .Select(p => new GpxTrackPoint(
                Coordinate(p, "lat"),
                Coordinate(p, "lon"),
                Number(Child(p, "ele")),
                Child(p, "time") is { } time
                    ? DateTime.Parse(time, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)
                    : null))
            .ToList();
        if (parsed.Count > 0)
        {
            segments.Add(parsed);
        }
    }

    private static XElement PointElement(string name, double lat, double lon, double? ele, DateTime? time) =>
        new(
            Ns + name,
            new XAttribute("lat", lat.ToString("0.0000000", CultureInfo.InvariantCulture)),
            new XAttribute("lon", lon.ToString("0.0000000", CultureInfo.InvariantCulture)),
            ele is null ? null : new XElement(Ns + "ele", ele.Value.ToString("0.0", CultureInfo.InvariantCulture)),
            time is null ? null : new XElement(Ns + "time", time.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)));

    // GPX 1.0 and 1.1 use different namespaces, so match on local names only.
    private static IEnumerable<XElement> Elements(XElement parent, string localName) =>
        parent.Elements().Where(e => e.Name.LocalName == localName);

    private static string? Child(XElement parent, string localName) =>
        Elements(parent, localName).FirstOrDefault()?.Value.Trim() is { Length: > 0 } value ? value : null;

    private static double Coordinate(XElement element, string attribute) =>
        Number(element.Attribute(attribute)?.Value)
        ?? throw new FormatException($"<{element.Name.LocalName}> is missing its '{attribute}' attribute.");

    private static double? Number(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
}
