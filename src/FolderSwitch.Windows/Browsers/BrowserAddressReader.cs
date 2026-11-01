using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Diagnostics;
using FolderSwitch.Core.Services;
using FolderSwitch.Core.Web;
using FolderSwitch.Windows.SystemServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace FolderSwitch.Windows.Browsers;

/// <summary>
/// Reads the address and the title of the page a Firefox, Chrome or Edge window shows, through COM UI Automation.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is read.</b> The address bar: Firefox's <c>urlbar-input</c> (by <c>AutomationId</c>), the
/// <c>OmniboxViewViews</c> of Chrome and Edge (by <c>ClassName</c>). Then the document of the active tab: the
/// <c>RootWebArea</c> elements of Chrome and Edge (a side panel or a split view has its own), the document under
/// Firefox's selected tab panel. The document whose address matches the bar gives the complete, encoded address and
/// the title of the page; the window title only stands in for a title the document does not give. Chrome and Edge
/// expose their documents only while their window is visible (not covered by other windows): otherwise the bar is
/// read alone. See <see cref="Resolve"/> for the rule.
/// </para>
/// <para>
/// <b>Series.</b> A result depends on the earlier attempts on the same window: a bar that no document confirms is
/// only trusted alone once that has lasted <see cref="DocumentGrace"/> within one series of attempts
/// (<see cref="DocumentGraceTracker"/>).
/// </para>
/// <para>
/// <b>Threads.</b> <see cref="TryRead"/> may be called from any thread; reads are serialized (a concurrent call
/// queues behind the current one). The UI Automation calls run on an STA thread owned by the reader, which the caller
/// waits for at most <see cref="AttemptTimeout"/>; UI Automation's own connection and transaction timeouts bound each
/// call to the same value. The thread releases its UI Automation objects when it ends.
/// </para>
/// <para>
/// <b>Overruns.</b> An attempt that overruns is abandoned with its thread, which ends once its current call returns
/// (each one is bounded by UI Automation's timeouts). While it is still alive, attempts give <c>null</c> at once; once
/// it ends, the next attempt starts a new thread.
/// </para>
/// <para>
/// <b>Failures.</b> Nothing is thrown: a zero or closed window, a window that is not the browser's, a page that is not
/// an <c>http</c> or <c>https</c> page, or any exception (a window closed during the read gives a
/// <see cref="COMException"/>) gives <c>null</c>.
/// </para>
/// </remarks>
public sealed class BrowserAddressReader : IBrowserAddressReader, IDisposable
{
    /// <summary>The time an attempt may take.</summary>
    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// How long a window may go, within a series of attempts, without a page document that confirms its address bar
    /// before the bar is trusted alone. Chrome and Edge may only expose the document once a first request has turned
    /// their accessibility on, and a page that is still loading has none yet: meanwhile the bar of Chrome and Edge,
    /// without its scheme, would turn an <c>http</c> page into an <c>https</c> one, and the window title may not carry
    /// the page's title yet.
    /// </summary>
    internal static readonly TimeSpan DocumentGrace = TimeSpan.FromSeconds(1);

    private const string WorkerName = "FolderSwitch browser address";

    private readonly Lock _gate = new();
    private readonly TimeProvider _time;
    private readonly long _origin;
    private readonly DocumentGraceTracker _grace = new(DocumentGrace);
    private ReadWorker? _worker;

    /// <summary>The worker given up on after an overrun, while it may still be running its attempt.</summary>
    private ReadWorker? _abandoned;

    private bool _disposed;

    public BrowserAddressReader()
        : this(TimeProvider.System)
    {
    }

    /// <summary>Test seam: a reader on another clock (the one that times the series of attempts).</summary>
    internal BrowserAddressReader(TimeProvider time)
    {
        _time = time;
        _origin = time.GetTimestamp();
    }

    /// <inheritdoc/>
    public BrowserPage? TryRead(nint window, BrowserKind browser)
    {
        try
        {
            if (window == 0 || !PInvoke.IsWindow((HWND)window))
            {
                return null;
            }

            lock (_gate)
            {
                if (_disposed)
                {
                    return null;
                }

                if (_abandoned is not null)
                {
                    if (_abandoned.Thread.IsAlive)
                    {
                        return null;
                    }

                    _abandoned = null;
                }

                var worker = _worker ??= new ReadWorker();
                var start = Now();
                if (!worker.Thread.TryRun(() => worker.Read(window, browser), AttemptTimeout, out var reading))
                {
                    Abandon();
                    return null;
                }

                if (reading is null)
                {
                    return null;
                }

                var trust = _grace.Trust(window, MatchingDocument(reading) is not null, start, Now());
                return Resolve(reading, browser, trust);
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Stops the reading thread. Later attempts give <c>null</c>.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _worker?.Thread.Dispose();
            _worker = null;
        }
    }

    /// <summary>
    /// The page an attempt read, or <c>null</c>:
    /// <list type="number">
    /// <item>no address bar, or an empty one, gives <c>null</c>;</item>
    /// <item>the web document whose address, scheme aside, is the bar's text (normalized, so that Firefox's decoded
    /// address and a root address without its <c>/</c> match) confirms the bar: its address is kept, complete and
    /// encoded, where the bar of Chrome and Edge hides the scheme;</item>
    /// <item>a bar that no document confirms (no document yet, an empty one, <c>about:blank</c> while the page loads,
    /// only a side panel's, text being typed) is completed by <see cref="WebAddress.TryNormalize"/>, which gives
    /// <c>https://</c> to text without a scheme. The confirming document may come at the next attempt: until
    /// <paramref name="trustAddressAlone"/>, such a bar gives <c>null</c>, except a bar with its scheme when a web
    /// document of another page is there (an address typed in full) or when the window title gives a title;</item>
    /// <item>what is not an <c>http</c> or <c>https</c> address gives <c>null</c>.</item>
    /// </list>
    /// The title is the name of the confirming document, or else the window title without the browser's suffix
    /// (<see cref="BrowserTitle.PageTitle"/>): Edge's window title says « … et N pages de plus » with several tabs.
    /// </summary>
    internal static BrowserPage? Resolve(Reading reading, BrowserKind browser, bool trustAddressAlone)
    {
        var address = reading.Address?.Trim();
        if (string.IsNullOrEmpty(address))
        {
            return null;
        }

        var document = MatchingDocument(reading);
        if (!WebAddress.TryNormalize(document?.Url ?? address, out var url))
        {
            return null;
        }

        var title = document is not null && !string.IsNullOrWhiteSpace(document.Name)
            ? document.Name.Trim()
            : BrowserTitle.PageTitle(reading.WindowTitle, browser);
        if (document is null && !trustAddressAlone
            && (!WebAddress.IsWebUrl(address) || (!reading.Documents.Any(d => WebAddress.IsWebUrl(d.Url)) && title is null)))
        {
            return null;
        }

        return new BrowserPage(url, title);
    }

    /// <summary>The web document that confirms the address bar, or <c>null</c>.</summary>
    internal static PageDocument? MatchingDocument(Reading reading)
    {
        var address = reading.Address?.Trim();
        return string.IsNullOrEmpty(address)
            ? null
            : reading.Documents.FirstOrDefault(d => WebAddress.IsWebUrl(d.Url) && SamePage(d.Url, address));
    }

    /// <summary>Whether both addresses, their <c>http</c> or <c>https</c> scheme removed, are the same once normalized.</summary>
    private static bool SamePage(string documentUrl, string address) =>
        WebAddress.SameSite("https://" + WithoutScheme(documentUrl), "https://" + WithoutScheme(address));

    private static string WithoutScheme(string address)
    {
        foreach (var scheme in (ReadOnlySpan<string>)["http://", "https://"])
        {
            if (address.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            {
                return address[scheme.Length..];
            }
        }

        return address;
    }

    /// <summary>The time on the reader's clock since it was created.</summary>
    private TimeSpan Now() => _time.GetElapsedTime(_origin);

    /// <summary>Under the gate: the worker overran; its thread is left to finish on its own, and no other starts until it has.</summary>
    private void Abandon()
    {
        if (_worker is not { } worker)
        {
            return;
        }

        worker.Thread.Dispose();
        _abandoned = worker;
        _worker = null;
    }

    /// <summary>A page document of the window.</summary>
    /// <param name="Url">Its address (not empty).</param>
    /// <param name="Name">Its name: the title of the page.</param>
    internal sealed record PageDocument(string Url, string? Name);

    /// <summary>What an attempt read from the window.</summary>
    /// <param name="Address">The text of the address bar.</param>
    /// <param name="Documents">The page documents found: Firefox's active tab; every <c>RootWebArea</c> of Chrome and Edge.</param>
    /// <param name="WindowTitle">The title of the window.</param>
    internal sealed record Reading(string? Address, IReadOnlyList<PageDocument> Documents, string? WindowTitle)
    {
        /// <summary>A reading with one document, or none when <paramref name="documentUrl"/> is <c>null</c>.</summary>
        public Reading(string? address, string? documentUrl, string? documentName, string? windowTitle)
            : this(address, documentUrl is null ? [] : [new PageDocument(documentUrl, documentName)], windowTitle)
        {
        }
    }

    /// <summary>A reading thread and its UI Automation objects, which only that thread touches.</summary>
    private sealed class ReadWorker
    {
        private const string FirefoxAddressId = "urlbar-input";
        private const string FirefoxTabPanelsId = "tabbrowser-tabpanels";
        private const string FirefoxSelectedPanelClass = "deck-selected";
        private const string ChromiumAddressClass = "OmniboxViewViews";
        private const string ChromiumDocumentId = "RootWebArea";

        private IUIAutomation2? _automation;
        private IUIAutomationCondition? _firefoxAddress;
        private IUIAutomationCondition? _firefoxTabPanels;
        private IUIAutomationCondition? _firefoxSelectedPanel;
        private IUIAutomationCondition? _document;
        private IUIAutomationCondition? _chromiumAddress;
        private IUIAutomationCondition? _chromiumDocument;

        /// <summary>
        /// Searches the raw view: until Chrome and Edge have turned on the accessibility of their web content, their
        /// <c>RootWebArea</c> is not a control element, and a search of the control view misses it. Found in the raw
        /// view, it gives its address and its name at once (or at the next attempt).
        /// </summary>
        private IUIAutomationCacheRequest? _rawView;

        public ReadWorker()
        {
            Thread = new StaWorker(WorkerName, Release);
        }

        public StaWorker Thread { get; }

        /// <summary>On the worker thread: what the window shows, or <c>null</c> when it has no address bar.</summary>
        public Reading? Read(nint window, BrowserKind browser)
        {
            // IUIAutomation2 comes with Windows 8: always there on the systems FolderSwitch supports.
            if (!OperatingSystem.IsWindowsVersionAtLeast(8))
            {
                return null;
            }

            var automation = Automation();
            var owned = new List<object>();
            try
            {
                var root = Own(owned, automation.ElementFromHandle((HWND)window));
                if (root is null)
                {
                    return null;
                }

                var bar = Own(owned, root.FindFirst(TreeScope.TreeScope_Descendants, browser == BrowserKind.Firefox ? _firefoxAddress : _chromiumAddress));
                if (bar is null)
                {
                    return null;
                }

                var address = ValueOf(bar, owned);
                var documents = new List<PageDocument>();
                try
                {
                    foreach (var element in Documents(root, browser, owned))
                    {
                        // Empty while Chrome or Edge is still turning its accessibility on: no document yet.
                        if (ValueOf(element, owned) is { Length: > 0 } url)
                        {
                            documents.Add(new PageDocument(url, Take(element.CurrentName)));
                        }
                    }
                }
                catch (Exception e) when (!CriticalExceptions.IsCritical(e))
                {
                    // The page is changing, the window closing, a UI Automation timeout (a TimeoutException): the bar
                    // is resolved with the documents read so far.
                }

                return new Reading(address, documents, WindowText(window));
            }
            finally
            {
                foreach (var com in owned)
                {
                    ReleaseQuietly(com);
                }
            }
        }

        /// <summary>The automation object and the search conditions, created on first use on this thread.</summary>
        [SupportedOSPlatform("windows8.0")]
        private IUIAutomation2 Automation()
        {
            if (_automation is { } existing)
            {
                return existing;
            }

            var automation = (IUIAutomation2)new CUIAutomation8();
            automation.ConnectionTimeout = (uint)AttemptTimeout.TotalMilliseconds;
            automation.TransactionTimeout = (uint)AttemptTimeout.TotalMilliseconds;

            _firefoxAddress = automation.CreatePropertyCondition(UIA_PROPERTY_ID.UIA_AutomationIdPropertyId, FirefoxAddressId);
            _firefoxTabPanels = automation.CreatePropertyCondition(UIA_PROPERTY_ID.UIA_AutomationIdPropertyId, FirefoxTabPanelsId);

            // Its class is « browserSidebarContainer deck-selected »: matched on the word that marks the selected panel.
            // Substring matching comes with Windows 10 1809; before, no Firefox document, so the window title is used.
            try
            {
                _firefoxSelectedPanel = automation.CreatePropertyConditionEx(
                    UIA_PROPERTY_ID.UIA_ClassNamePropertyId, FirefoxSelectedPanelClass, PropertyConditionFlags.PropertyConditionFlags_MatchSubstring);
            }
            catch (ArgumentException)
            {
                _firefoxSelectedPanel = null;
            }

            _document = automation.CreatePropertyCondition(UIA_PROPERTY_ID.UIA_ControlTypePropertyId, (int)UIA_CONTROLTYPE_ID.UIA_DocumentControlTypeId);
            _chromiumAddress = automation.CreatePropertyCondition(UIA_PROPERTY_ID.UIA_ClassNamePropertyId, ChromiumAddressClass);
            _chromiumDocument = automation.CreatePropertyCondition(UIA_PROPERTY_ID.UIA_AutomationIdPropertyId, ChromiumDocumentId);
            _rawView = automation.CreateCacheRequest();
            _rawView.TreeFilter = automation.RawViewCondition;
            _automation = automation;
            return automation;
        }

        /// <summary>
        /// On the worker thread, once it stops taking work: releases the UI Automation objects it created (best effort;
        /// their wrappers would otherwise wait for the finalizer).
        /// </summary>
        private void Release()
        {
            object?[] objects =
            [
                _rawView, _chromiumDocument, _chromiumAddress, _document, _firefoxSelectedPanel, _firefoxTabPanels,
                _firefoxAddress, _automation,
            ];
            _rawView = null;
            _chromiumDocument = _chromiumAddress = _document = _firefoxSelectedPanel = _firefoxTabPanels = _firefoxAddress = null;
            _automation = null;
            foreach (var com in objects)
            {
                ReleaseQuietly(com);
            }
        }

        /// <summary>The page documents: every <c>RootWebArea</c> of Chrome and Edge; the document of Firefox's active tab.</summary>
        private List<IUIAutomationElement> Documents(IUIAutomationElement root, BrowserKind browser, List<object> owned)
        {
            var documents = new List<IUIAutomationElement>();
            if (browser != BrowserKind.Firefox)
            {
                var found = Own(owned, root.FindAllBuildCache(TreeScope.TreeScope_Descendants, _chromiumDocument, _rawView));
                var count = found?.Length ?? 0;
                for (var i = 0; i < count; i++)
                {
                    if (Own(owned, found!.GetElement(i)) is { } element)
                    {
                        documents.Add(element);
                    }
                }

                return documents;
            }

            if (_firefoxSelectedPanel is null)
            {
                return documents;
            }

            var panels = Own(owned, root.FindFirst(TreeScope.TreeScope_Descendants, _firefoxTabPanels));
            var selected = Own(owned, panels?.FindFirst(TreeScope.TreeScope_Children, _firefoxSelectedPanel));
            if (Own(owned, selected?.FindFirstBuildCache(TreeScope.TreeScope_Descendants, _document, _rawView)) is { } document)
            {
                documents.Add(document);
            }

            return documents;
        }

        private static void ReleaseQuietly(object? com)
        {
            try
            {
                if (com is not null && Marshal.IsComObject(com))
                {
                    Marshal.ReleaseComObject(com);
                }
            }
            catch (Exception e) when (!CriticalExceptions.IsCritical(e))
            {
                // Best effort.
            }
        }

        /// <summary>The value of the element's <c>ValuePattern</c>, <c>null</c> when it has none.</summary>
        private static string? ValueOf(IUIAutomationElement element, List<object> owned) =>
            Own(owned, element.GetCurrentPattern(UIA_PATTERN_ID.UIA_ValuePatternId)) is IUIAutomationValuePattern pattern
                ? Take(pattern.CurrentValue)
                : null;

        /// <summary>The string of a <see cref="BSTR"/> the caller owns, which is freed.</summary>
        private static string? Take(BSTR value)
        {
            try
            {
                return value.ToString();
            }
            finally
            {
                Marshal.FreeBSTR(value);
            }
        }

        private static T? Own<T>(List<object> owned, T? com)
            where T : class
        {
            if (com is not null && Marshal.IsComObject(com))
            {
                owned.Add(com);
            }

            return com;
        }

        private static unsafe string? WindowText(nint window)
        {
            var length = PInvoke.GetWindowTextLength((HWND)window);
            if (length <= 0)
            {
                return null;
            }

            var buffer = new char[length + 1];
            fixed (char* text = buffer)
            {
                var copied = PInvoke.GetWindowText((HWND)window, text, buffer.Length);
                return new string(text, 0, Math.Max(copied, 0));
            }
        }
    }
}
