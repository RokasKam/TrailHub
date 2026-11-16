using TrailHub.Notifications.Application.Requests;
using TrailHub.Notifications.Application.Responses;

namespace TrailHub.Notifications.Application.Interfaces.Service;

/// <summary>The caller's push switches per category.</summary>
public interface INotificationPreferencesService
{
    Task<PreferencesResponse> GetAsync(CancellationToken cancellationToken);

    Task<PreferencesResponse> UpdateAsync(PreferencesRequest request, CancellationToken cancellationToken);
}
