using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TrailHub.Notifications.Application.Models;
using TrailHub.Notifications.Infrastructure.Push;

namespace TrailHub.UnitTests;

public sealed class ExpoPushSenderTests
{
    private static readonly Guid NotificationId = Guid.NewGuid();

    private static PushMessage Message(string token) => new(token, "New like", "Ben liked your hike Rysy ridge", "/feed/1", NotificationId, 3);

    private static (ExpoPushSender Sender, FakeHttpMessageHandler Handler) Create(Func<int, (HttpStatusCode, string)> respond)
    {
        var handler = new FakeHttpMessageHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://exp.host/") };
        return (new ExpoPushSender(http, NullLogger<ExpoPushSender>.Instance), handler);
    }

    [Fact]
    public async Task Sends_the_message_shape_expo_expects()
    {
        var (sender, handler) = Create(_ => (HttpStatusCode.OK, """{"data":[{"status":"ok","id":"r1"}]}"""));

        var gone = await sender.SendAsync([Message("ExponentPushToken[a]")], default);

        Assert.Empty(gone);
        Assert.Equal("https://exp.host/--/api/v2/push/send", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        var m = JsonDocument.Parse(handler.Bodies[0]).RootElement[0];
        Assert.Equal("ExponentPushToken[a]", m.GetProperty("to").GetString());
        Assert.Equal("New like", m.GetProperty("title").GetString());
        Assert.Equal("Ben liked your hike Rysy ridge", m.GetProperty("body").GetString());
        Assert.Equal("/feed/1", m.GetProperty("data").GetProperty("url").GetString());
        Assert.Equal(NotificationId, m.GetProperty("data").GetProperty("notificationId").GetGuid());
        Assert.Equal("default", m.GetProperty("sound").GetString());
        Assert.Equal(3, m.GetProperty("badge").GetInt32());
    }

    [Fact]
    public async Task DeviceNotRegistered_tickets_report_the_token()
    {
        var (sender, _) = Create(_ => (HttpStatusCode.OK, """
            {"data":[
              {"status":"ok","id":"r1"},
              {"status":"error","message":"not registered","details":{"error":"DeviceNotRegistered"}},
              {"status":"error","message":"too big","details":{"error":"MessageTooBig"}}
            ]}
            """));

        var gone = await sender.SendAsync([Message("ExponentPushToken[a]"), Message("ExponentPushToken[b]"), Message("ExponentPushToken[c]")], default);

        Assert.Equal(["ExponentPushToken[b]"], gone);
    }

    [Fact]
    public async Task Sends_at_most_100_per_request()
    {
        var (sender, handler) = Create(i => (HttpStatusCode.OK, JsonSerializer.Serialize(new { data = Enumerable.Range(0, i == 0 ? 100 : 50).Select(_ => new { status = "ok" }) })));

        await sender.SendAsync(Enumerable.Range(0, 150).Select(i => Message($"ExponentPushToken[{i}]")).ToList(), default);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(100, JsonDocument.Parse(handler.Bodies[0]).RootElement.GetArrayLength());
        Assert.Equal(50, JsonDocument.Parse(handler.Bodies[1]).RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Request_errors_are_logged_not_thrown()
    {
        var (sender, _) = Create(_ => (HttpStatusCode.TooManyRequests, """{"errors":[{"code":"TOO_MANY_REQUESTS","message":"slow down"}]}"""));

        Assert.Empty(await sender.SendAsync([Message("ExponentPushToken[a]")], default));
    }

    [Fact]
    public async Task Unreadable_responses_are_logged_not_thrown()
    {
        var (sender, _) = Create(_ => (HttpStatusCode.BadGateway, "<html>bad gateway</html>"));

        Assert.Empty(await sender.SendAsync([Message("ExponentPushToken[a]")], default));
    }
}
