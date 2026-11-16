using System.Net;
using System.Text;

namespace TrailHub.UnitTests;

/// <summary>Answers every request with a canned response and records the request bodies.</summary>
internal sealed class FakeHttpMessageHandler(Func<int, (HttpStatusCode Status, string Json)> respond) : HttpMessageHandler
{
    public List<string> Bodies { get; } = [];

    public List<HttpRequestMessage> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
        var (status, json) = respond(Requests.Count - 1);
        return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
