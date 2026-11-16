using TrailHub.Notifications.Application.Requests;

namespace TrailHub.Notifications.Application.Interfaces.Service;

/// <summary>The caller's phones that receive pushes.</summary>
public interface IPushDeviceService
{
    /// <summary>Adds or refreshes the token for the caller, taking it over if it belonged to someone else.</summary>
    Task RegisterAsync(RegisterDeviceRequest request, CancellationToken cancellationToken);

    /// <summary>Forgets the caller's token (sign-out). Unknown tokens and other people's are ignored.</summary>
    Task UnregisterAsync(string token, CancellationToken cancellationToken);
}
