using TrailHub.BuildingBlocks.Application;
using TrailHub.Notifications.Application.Interfaces.Repository;
using TrailHub.Notifications.Application.Interfaces.Service;
using TrailHub.Notifications.Application.Mappings;
using TrailHub.Notifications.Application.Requests;
using TrailHub.Notifications.Application.Responses;
using TrailHub.Notifications.Domain.Preferences;

namespace TrailHub.Notifications.Application.Services;

internal sealed class NotificationPreferencesService(
    INotificationPreferencesRepository preferences,
    IUnitOfWork unitOfWork,
    ICurrentUser user) : INotificationPreferencesService
{
    public async Task<PreferencesResponse> GetAsync(CancellationToken cancellationToken) =>
        (await preferences.GetAsync(user.UserId, cancellationToken) ?? NotificationPreferences.Default(user.UserId)).ToResponse();

    public async Task<PreferencesResponse> UpdateAsync(PreferencesRequest request, CancellationToken cancellationToken)
    {
        var prefs = await preferences.GetAsync(user.UserId, cancellationToken);
        if (prefs is null)
        {
            prefs = NotificationPreferences.Default(user.UserId);
            preferences.Add(prefs);
        }

        prefs.Update(request.LikesComments, request.Group, request.Trips);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return prefs.ToResponse();
    }
}
