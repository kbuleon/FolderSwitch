// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FolderSwitch.Windows.IntegrationTests.Support;

/// <summary>
/// A web server on <c>http://localhost:&lt;port&gt;/</c> for the browsers a test starts: it serves the HTML pages it is
/// given, by path, and answers 404 to anything else. Nothing leaves the machine. Stopped on dispose.
/// </summary>
internal sealed class LocalWebServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Dictionary<string, string> _pages;
    private readonly Task _loop;

    /// <param name="pages">The HTML of each page, by its path (<c>/page</c>).</param>
    public LocalWebServer(IReadOnlyDictionary<string, string> pages)
    {
        _pages = new Dictionary<string, string>(pages, StringComparer.Ordinal);
        Port = FreePort();
        _listener.Prefixes.Add($"http://localhost:{Port}/");
        _listener.Start();
        _loop = Task.Run(ServeAsync);
    }

    public int Port { get; }

    /// <summary>The address of <paramref name="path"/> on this server.</summary>
    public string Url(string path) => $"http://localhost:{Port}{path}";

    /// <summary>A minimal HTML page with this title.</summary>
    public static string Page(string title) =>
        $"<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>{WebUtility.HtmlEncode(title)}</title></head>"
        + $"<body><h1>{WebUtility.HtmlEncode(title)}</h1></body></html>";

    public void Dispose()
    {
        _listener.Close();
        try
        {
            _loop.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // The loop ends on the listener's own exception once it is closed.
        }
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try
        {
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }
        finally
        {
            probe.Stop();
        }
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            try
            {
                var response = context.Response;
                if (_pages.TryGetValue(context.Request.Url?.AbsolutePath ?? string.Empty, out var html))
                {
                    var body = Encoding.UTF8.GetBytes(html);
                    response.ContentType = "text/html; charset=utf-8";
                    response.ContentLength64 = body.Length;
                    await response.OutputStream.WriteAsync(body).ConfigureAwait(false);
                }
                else
                {
                    response.StatusCode = 404;
                }

                response.Close();
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or IOException)
            {
                // The browser went away mid-response.
            }
        }
    }
}
