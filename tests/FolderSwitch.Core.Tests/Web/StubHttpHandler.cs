// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Net;
using System.Text;

namespace FolderSwitch.Core.Tests.Web;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that answers from a table of address to response, records every request, and
/// never touches the network. Redirects are returned as they are: the code under test follows them (or not).
/// </summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> routes = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private readonly List<HttpRequestMessage> requests = [];

    /// <summary>Answers for addresses with no route; <c>404</c> by default.</summary>
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? Fallback { get; set; }

    public IReadOnlyList<HttpRequestMessage> Requests
    {
        get
        {
            lock (gate)
            {
                return [.. requests];
            }
        }
    }

    public IEnumerable<string> RequestedUrls => Requests.Select(r => r.RequestUri!.AbsoluteUri);

    public StubHttpHandler On(string url, Func<HttpResponseMessage> response)
    {
        routes[url] = response;
        return this;
    }

    public StubHttpHandler OnHtml(string url, string html) =>
        On(url, () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") });

    public StubHttpHandler OnBytes(string url, byte[] data) =>
        On(url, () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) });

    public StubHttpHandler OnRedirect(string url, string location, HttpStatusCode status = HttpStatusCode.Found) =>
        On(url, () =>
        {
            var response = new HttpResponseMessage(status);
            response.Headers.TryAddWithoutValidation("Location", location);
            return response;
        });

    public StubHttpHandler OnStatus(string url, HttpStatusCode status) => On(url, () => new HttpResponseMessage(status));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            requests.Add(request);
        }

        if (routes.TryGetValue(request.RequestUri!.AbsoluteUri, out var response))
        {
            return response();
        }

        if (Fallback is not null)
        {
            return await Fallback(request, cancellationToken);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}

/// <summary>A response body whose length is not known in advance (chunked), as a server may send it.</summary>
internal sealed class UnknownLengthContent(byte[] data) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        stream.WriteAsync(data, 0, data.Length);

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
