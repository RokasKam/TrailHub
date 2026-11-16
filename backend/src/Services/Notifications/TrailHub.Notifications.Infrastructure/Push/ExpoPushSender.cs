using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrailHub.Notifications.Application.Interfaces.Providers;
using TrailHub.Notifications.Application.Models;

namespace TrailHub.Notifications.Infrastructure.Push;

/// <summary>
/// Sends pushes through the Expo push API (<c>POST /--/api/v2/push/send</c>), at most 100 messages per request.
/// Tickets with <c>DeviceNotRegistered</c> report the token as gone. Failures are logged, never thrown.
/// See https://docs.expo.dev/push-notifications/sending-notifications/.
/// </summary>
internal sealed class ExpoPushSender(HttpClient http, ILogger<ExpoPushSender> logger) : IPushSender
{
    public const int MaxMessagesPerRequest = 100;
    public const string SendPath = "--/api/v2/push/send";
    private const string DeviceNotRegistered = "DeviceNotRegistered";

    /// <summary>The Android channel the app creates; without a matching channel Android doesn't show the notification.</summary>
    private const string AndroidChannel = "default";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken cancellationToken)
    {
        var unregistered = new List<string>();
        foreach (var chunk in messages.Chunk(MaxMessagesPerRequest))
        {
            try
            {
                unregistered.AddRange(await SendChunkAsync(chunk, cancellationToken));
            }
            catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(e, "Sending {Count} push notification(s) failed", chunk.Length);
            }
        }

        return unregistered;
    }

    private async Task<IReadOnlyList<string>> SendChunkAsync(PushMessage[] chunk, CancellationToken cancellationToken)
    {
        var body = chunk
            .Select(m => new ExpoMessage(m.To, m.Title, m.Body, new ExpoMessageData(m.Url, m.NotificationId), "default", m.Badge, AndroidChannel))
            .ToList();
        using var response = await http.PostAsJsonAsync(SendPath, body, Json, cancellationToken);
        var result = await ReadAsync(response, cancellationToken);
        if (!response.IsSuccessStatusCode || result?.Data is null)
        {
            var error = result?.Errors is [var first, ..] ? first : null;
            logger.LogWarning("Expo push request failed: {Status} {Code} {Message}", (int)response.StatusCode, error?.Code, error?.Message);
            return [];
        }

        var gone = new List<string>();
        for (var i = 0; i < result.Data.Count && i < chunk.Length; i++)
        {
            var ticket = result.Data[i];
            if (ticket.Status != "error")
            {
                continue;
            }

            if (ticket.Details?.Error == DeviceNotRegistered)
            {
                gone.Add(ticket.Details.ExpoPushToken ?? chunk[i].To);
            }
            else
            {
                logger.LogWarning("Expo rejected a push: {Error} {Message}", ticket.Details?.Error, ticket.Message);
            }
        }

        return gone;
    }

    private static async Task<ExpoSendResponse?> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ExpoSendResponse>(Json, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
