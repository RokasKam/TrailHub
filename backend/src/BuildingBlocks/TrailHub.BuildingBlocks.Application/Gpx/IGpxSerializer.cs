using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.BuildingBlocks.Application.Gpx;

public interface IGpxSerializer
{
    Result<GpxDocument> Parse(Stream gpx);

    string Write(GpxDocument document);
}
