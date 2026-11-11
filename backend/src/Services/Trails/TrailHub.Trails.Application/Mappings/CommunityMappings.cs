using TrailHub.Trails.Application.Responses;
using TrailHub.Trails.Domain.Community;

namespace TrailHub.Trails.Application.Mappings;

internal static class CommunityMappings
{
    public static ReviewResponse ToResponse(this Review r) => new(r.Id, r.UserId, r.Rating, r.Comment, r.CreatedAtUtc, r.UpdatedAtUtc);
}
