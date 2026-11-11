using TrailHub.Groups.Application.Dtos;

namespace TrailHub.Groups.Api.Hubs;

/// <summary>Server → client messages. Typed so the contract with the app lives in one place.</summary>
public interface ILocationClient
{
    Task LocationUpdated(Guid groupId, LiveLocationDto location);

    Task LocationRemoved(Guid groupId, string userId);
}
