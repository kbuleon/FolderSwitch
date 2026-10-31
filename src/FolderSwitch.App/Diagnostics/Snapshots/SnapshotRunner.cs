using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FolderSwitch.App.Themes;
using FolderSwitch.App.Views;
using FolderSwitch.App.Views.DragDrop;
using FolderSwitch.App.Views.Layout;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Persistence;
using FolderSwitch.Core.Theming;
using FolderSwitch.Presentation.ViewModels;
using ThemeMode = FolderSwitch.Core.Model.ThemeMode;

namespace FolderSwitch.App.Diagnostics.Snapshots;

/// <summary>
/// <c>--snapshot &lt;directory&gt;</c>: draws the content of the main window (<see cref="MainView"/>) and of the first-run
/// wizard (<see cref="WizardView"/>) off-screen, without any window, into PNG files, so that its look can be checked without showing anything on the user's screen. The view
/// models are built on <see cref="SnapshotSample"/> with services that do nothing (<see cref="SnapshotServices"/>).
/// </summary>
/// <remarks>
/// <para>
/// Drawn at 96 and at 144 DPI (<c>…-144dpi.png</c>), in the language of <c>--snapshot-language &lt;code&gt;</c>
/// (French by default): every theme in both variants, in normal use (<c>&lt;theme&gt;-&lt;variant&gt;.png</c>); then
/// Customize mode with an empty column (<c>…-customize.png</c>), the context line (<c>…-context.png</c>), long names in
/// narrow columns (<c>…-narrow.png</c>), a board larger than the work area (<c>…-overflow.png</c>) and twelve tabs that
/// wrap in a narrow work area, in normal use and in Customize mode (<c>…-manytabs.png</c>,
/// <c>…-manytabs-customize.png</c>). Another language than French adds its code to every name (<c>…-de.png</c>).
/// </para>
/// <para>
/// The interactions of Customize mode, in Fluent light and dark and in Cards dark (Fluent light only at 144 DPI): the
/// settings window on each of its pages (<c>…-settings-general</c>, <c>…-settings-hotkey</c> with a combination not
/// applied yet, <c>…-settings-hotkey-applied</c> with the shortcut in effect, <c>…-settings-appearance</c>,
/// <c>…-settings-configuration</c>), the About window (<c>…-about</c>), the
/// « + Folder ▾ » menu of a group holding special folders (<c>…-addfolder</c>), the palette (<c>…-palette</c>), a rename
/// in progress (<c>…-rename</c>), the insertion line of an item being dragged (<c>…-drag</c>), a confirmation
/// (<c>…-confirm</c>), and in Fluent light a column being resized (<c>…-resize</c>). Menus are drawn where their popup
/// would be (<see cref="MainView.FlyoutLayer"/>), the dialog centered on the window as an owned one would be.
/// </para>
/// <para>
/// The first-run wizard (<see cref="WizardView"/>, in its own look, <see cref="WizardTheme"/>), Fluent light, at both
/// scales (<c>wizard-&lt;step&gt;[-&lt;case&gt;][-144dpi][-&lt;language&gt;].png</c>): its four steps (<c>1-language</c>,
/// <c>2-hotkey</c>, <c>3-appearance</c>, <c>4-startup</c>), the shortcut with a warning (Ctrl + Alt + F,
/// <c>2-hotkey-ctrlalt</c>) and with a refusal (⊞ Win + L, <c>2-hotkey-reserved</c>), the appearance step in Fluent dark
/// (<c>3-appearance-dark</c>) and the startup step with starting with Windows unchecked (<c>4-startup-noautostart</c>).
/// </para>
/// <para>
/// One view is kept for each scale and the appearance is changed under it, as in the running application: a view that
/// did not follow a change (a group that kept its colors) would show in the next file. The application only tells its
/// windows that its resources changed, and this view is in none: the theme's resources are also put at the root of the
/// view's tree before each file, which makes it look them up again as a window does. Each file shows the view in a
/// window frame of its own (corners, border, shadow) on a neutral backdrop; the frame is the snapshot's, not the
/// system's.
/// </para>
/// <para>Exit code 0 when every file was written, 1 otherwise (the reason is on the error output and in the debug log, <c>folderswitch.log</c> in the output folder).</para>
/// </remarks>
internal static class SnapshotRunner
{
    public const string DefaultLanguage = "fr";

    /// <summary>The largest window content drawn, in DIPs: a common work area at 100 %.</summary>
    private static readonly Size WorkArea = new(1600, 1000);

    /// <summary>A narrow work area for the many-tabs scene: a small laptop screen at a high scale.</summary>
    private static readonly Size ManyTabsWorkArea = new(900, 700);

    private static readonly double[] Scales = [1.0, 1.5];

    /// <summary>Where the interactions of Customize mode are drawn.</summary>
    private static readonly (string Theme, Variant Variant)[] InteractionLooks =
        [("fluent", Variant.Light), ("fluent", Variant.Dark), ("cards", Variant.Dark)];

    private const string ExePath = @"C:\FolderSwitch\FolderSwitch.exe";

    private const double DeskPadding = 24;
    private const double FrameRadius = 8;
    private const double MinWindowWidth = 360;

    /// <summary>Layout passes before a file is drawn, at most: building, scaling, settling.</summary>
    private const int MaxPasses = 4;

    public static int Run(string directory, string? language)
    {
        try
        {
            var written = Render(directory, language ?? DefaultLanguage);
            DebugLog.Info($"Snapshot: {written} files written to {Path.GetFullPath(directory)}");
            Console.Out.WriteLine($"{written} snapshots written to {Path.GetFullPath(directory)}");
            return 0;
        }
        catch (Exception e)
        {
            DebugLog.Error("Snapshot failed", e);
            Console.Error.WriteLine($"Snapshot failed: {e}");
            return 1;
        }
    }

    private static int Render(string directory, string language)
    {
        var localizer = Localizer.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Strings"));
        var code = localizer.Available.Select(available => available.Code)
            .FirstOrDefault(available => available.Equals(language, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"The language '{language}' is not available.", nameof(language));
        localizer.SetLanguage(code);
        var suffix = code.Equals(DefaultLanguage, StringComparison.OrdinalIgnoreCase) ? string.Empty : "-" + code;

        Directory.CreateDirectory(directory);
        var application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        // The settings need a store: a throwaway folder, never the user's (nothing is exported or imported anyway).
        var storeDirectory = Path.Combine(Path.GetTempPath(), "FolderSwitch.Snapshot-" + Guid.NewGuid().ToString("N"));
        try
        {
            var services = new SnapshotServices();
            var config = SnapshotSample.Create();
            var main = new MainViewModel(config, services, services, services, services, localizer);
            var narrow = new MainViewModel(SnapshotSample.CreateNarrow(), services, services, services, services, localizer);
            var manyTabs = new MainViewModel(SnapshotSample.CreateManyTabs(), services, services, services, services, localizer);
            using var appearance = new AppearanceViewModel(config, services, services);
            using var applier = new ThemeApplier(appearance, application.Resources);

            // A shortcut with a warning, so that the settings show how a problem reads.
            using var hotkey = new HotkeyEditorViewModel(new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, 'F'), services, localizer);
            var settings = new SettingsViewModel(
                config, localizer, hotkey, services, services, ExePath,
                new ConfigStore(storeDirectory, TimeProvider.System), services, services, checkUpdates: null);
            var texts = new ViewTexts(localizer);

            var written = 0;
            foreach (var scale in Scales)
            {
                var clock = Stopwatch.StartNew();
                var view = new MainView { DataContext = main, Texts = texts, Appearance = appearance, Settings = settings };
                var scene = new Scene(view, () => ThemeApplier.Build(applier.Current), scale);
                scene.Layout();
                DebugLog.Info(FormattableString.Invariant($"Snapshot: view built and laid out in {clock.Elapsed.TotalMilliseconds:F0} ms at scale {scale}"));

                if (scale == 1.0)
                {
                    // What a press of Tab costs: the next tab's columns, groups and items are built. The first round pays
                    // for the code's first use too; the second one is what a press costs afterwards.
                    for (var round = 1; round <= 2; round++)
                    {
                        clock.Restart();
                        main.NextTab();
                        scene.Layout();
                        main.PreviousTab();
                        scene.Layout();
                        DebugLog.Info(FormattableString.Invariant($"Snapshot: two tab changes laid out in {clock.Elapsed.TotalMilliseconds:F0} ms (round {round})"));
                    }
                }

                foreach (var theme in ThemeCatalog.ThemeKeys)
                {
                    foreach (var variant in new[] { Variant.Light, Variant.Dark })
                    {
                        Use(appearance, theme, variant);
                        scene.Save(Path.Combine(directory, FileName(theme, variant, null, scale, suffix)), variant);
                        written++;
                    }
                }

                foreach (var (theme, variant) in new[] { ("fluent", Variant.Light), ("cards", Variant.Dark) })
                {
                    Use(appearance, theme, variant);
                    main.ToggleCustomize();

                    // An empty column: its ✕ shows.
                    main.AddColumn();
                    scene.Save(Path.Combine(directory, FileName(theme, variant, "customize", scale, suffix)), variant);
                    main.RemoveColumn(main.ActiveTab.Columns[^1]);
                    main.ToggleCustomize();
                    written++;
                }

                foreach (var (theme, variant) in InteractionLooks)
                {
                    if (scale != 1.0 && (theme, variant) != ("fluent", Variant.Light))
                    {
                        continue;
                    }

                    Use(appearance, theme, variant);
                    written += SaveInteractions(scene, main, services, directory, theme, variant, scale, suffix);
                }

                Use(appearance, "fluent", Variant.Light);
                main.OnShown(new CallContext(CallContextKind.Explorer, 0, @"D:\Clients\Acme\Contrats"));
                scene.Save(Path.Combine(directory, FileName("fluent", Variant.Light, "context", scale, suffix)), Variant.Light);
                main.OnShown(CallContext.None);
                written++;

                scene.View.DataContext = narrow;
                scene.Save(Path.Combine(directory, FileName("fluent", Variant.Light, "narrow", scale, suffix)), Variant.Light);
                scene.View.DataContext = main;
                written++;

                // Past the screen's work area, the board scrolls.
                scene.Bound(new Size(480, 300));
                scene.Save(Path.Combine(directory, FileName("fluent", Variant.Light, "overflow", scale, suffix)), Variant.Light);
                scene.Bound(null);
                written++;

                // More tabs than a line of a narrow work area holds: the tab bar wraps, and « + » stays in reach.
                scene.View.DataContext = manyTabs;
                scene.Bound(ManyTabsWorkArea);
                scene.Save(Path.Combine(directory, FileName("fluent", Variant.Light, "manytabs", scale, suffix)), Variant.Light);
                manyTabs.ToggleCustomize();
                scene.Save(Path.Combine(directory, FileName("fluent", Variant.Light, "manytabs-customize", scale, suffix)), Variant.Light);
                manyTabs.ToggleCustomize();
                scene.Bound(null);
                scene.View.DataContext = main;
                written += 2;

                written += SaveWizard(localizer, code, services, texts, directory, scale, suffix);
            }

            return written;
        }
        finally
        {
            DeleteQuietly(storeDirectory);
        }
    }

    /// <summary>The interactions of Customize mode in one look; returns the number of files written.</summary>
    private static int SaveInteractions(Scene scene, MainViewModel main, SnapshotServices services, string directory, string theme, Variant variant, double scale, string suffix)
    {
        string File(string kind) => Path.Combine(directory, FileName(theme, variant, kind, scale, suffix));

        var written = 0;
        main.ToggleCustomize();
        scene.Layout();
        var view = scene.View;

        // The settings window, page by page, then the About window.
        foreach (var (page, kind) in new[]
        {
            (SettingsPage.General, "settings-general"),
            (SettingsPage.Hotkey, "settings-hotkey"),
            (SettingsPage.Appearance, "settings-appearance"),
            (SettingsPage.Configuration, "settings-configuration"),
        })
        {
            view.OpenSettings(page);
            scene.Save(File(kind), variant);
            written++;
        }

        // The shortcut in effect: nothing to apply, a note says it is active. Then the combination with a warning again
        // (a capture « presses » it at once, see SnapshotServices.CaptureResult).
        var hotkey = view.Settings!.Hotkey;
        var shown = hotkey.Current;
        view.OpenSettings(SettingsPage.Hotkey);
        hotkey.ResetToDefault();
        scene.Save(File("settings-hotkey-applied"), variant);
        services.CaptureResult = shown;
        hotkey.StartCapture();
        services.CaptureResult = null;
        written++;

        view.CloseDialog();
        view.OpenAbout();
        scene.Save(File("about"), variant);
        view.CloseDialog();
        written++;

        // The « + Folder ▾ » menu of « Personnel », which holds Downloads, Documents and Desktop: those are checked.
        var personal = scene.Group("Personnel");
        view.OpenAddFolderMenu((GroupViewModel)personal.DataContext, personal.AddFolderAnchor);
        scene.Save(File("addfolder"), variant);
        view.CloseFlyout();
        written++;

        // The palette of « Projets » (blue).
        var projects = scene.Group("Projets");
        view.OpenPalette((GroupViewModel)projects.DataContext, projects.ColorDotAnchor);
        scene.Save(File("palette"), variant);
        view.CloseFlyout();
        written++;

        // A rename in progress: « Clients », the whole name selected.
        var clients = (GroupViewModel)scene.Group("Clients").DataContext;
        main.BeginRename(clients);
        scene.Save(File("rename"), variant);
        main.CancelRename(clients);
        main.Selected = null;
        written++;

        // « Maquettes » dragged from « Projets » to between the first two items of « Clients ».
        var dragged = scene.Item("Maquettes");
        var target = scene.Group("Clients");
        dragged.Opacity = 0.4;
        scene.Layout();
        BoardDragDrop.ShowIndicator(target.Items, 1, Orientation.Vertical);
        scene.Save(File("drag"), variant);
        BoardDragDrop.HideIndicator();
        dragged.Opacity = 1;
        written++;

        // The confirmation of a deletion, over the window.
        scene.ShowDialog(new PromptView(PromptKind.Confirm, Localized(scene, "Confirm.DeleteGroup"), key => Localized(scene, key)));
        scene.Save(File("confirm"), variant);
        scene.ShowDialog(null);
        written++;

        if (scale == 1.0 && (theme, variant) == ("fluent", Variant.Light))
        {
            // The second column being resized to 260: its width shows above it.
            var presenter = scene.Column(1).Presenter;
            presenter.PreviewWidth = 260;
            scene.Save(File("resize"), variant);
            presenter.PreviewWidth = null;
            written++;
        }

        main.ToggleCustomize();
        return written;
    }

    /// <summary>The first-run wizard's scenes at one scale; returns the number of files written.</summary>
    private static int SaveWizard(Localizer localizer, string code, SnapshotServices services, ViewTexts texts, string directory, double scale, string suffix)
    {
        using var wizard = new WizardViewModel(localizer, services, services, services, ExePath, services);
        var view = new WizardView();
        view.Attach(wizard, texts, windowsLanguage: code);

        // The snapshot's language, not the one the view model preselected from the services' Windows language (French):
        // chosen once the view is shown, as at step 1, so that another language shows the view following the change.
        wizard.Language = code;

        var variant = Variant.Light;
        var scene = new Scene(view, () => WizardTheme.Build("fluent", variant), scale);
        var written = 0;

        void Save(string kind)
        {
            scene.Save(Path.Combine(directory, WizardFileName(kind, scale, suffix)), variant);
            written++;
        }

        try
        {
            Save("1-language");

            wizard.Next();
            Save("2-hotkey");

            // A capture « presses » the combination at once (see SnapshotServices.CaptureResult).
            services.CaptureResult = new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, 'F');
            wizard.Hotkey.StartCapture();
            Save("2-hotkey-ctrlalt");

            services.CaptureResult = new Hotkey(KeyModifiers.Win, VirtualKeys.L);
            wizard.Hotkey.StartCapture();
            Save("2-hotkey-reserved");

            services.CaptureResult = null;
            wizard.Hotkey.ResetToDefault();
            wizard.Next();
            Save("3-appearance");

            wizard.Mode = ThemeMode.Dark;
            variant = Variant.Dark;
            Save("3-appearance-dark");
            wizard.Mode = ThemeMode.Light;
            variant = Variant.Light;

            wizard.Next();
            Save("4-startup");

            wizard.StartWithWindows = false;
            Save("4-startup-noautostart");
        }
        finally
        {
            services.CaptureResult = null;
            view.Dispose();
        }

        return written;
    }

    /// <summary><c>wizard-&lt;kind&gt;[-144dpi][-&lt;language&gt;].png</c>.</summary>
    private static string WizardFileName(string kind, double scale, string suffix) =>
        string.Create(CultureInfo.InvariantCulture, $"wizard-{kind}{(scale == 1.0 ? string.Empty : $"-{96 * scale:F0}dpi")}{suffix}.png");

    private static string Localized(Scene scene, string key) => scene.View.Texts?[key] ?? key;

    /// <summary><c>&lt;theme&gt;-&lt;variant&gt;[-&lt;kind&gt;][-144dpi][-&lt;language&gt;].png</c>.</summary>
    private static string FileName(string theme, Variant variant, string? kind, double scale, string suffix) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{theme}-{Name(variant)}{(kind is null ? string.Empty : "-" + kind)}{(scale == 1.0 ? string.Empty : $"-{96 * scale:F0}dpi")}{suffix}.png");

    private static void Use(AppearanceViewModel appearance, string theme, Variant variant)
    {
        appearance.Theme = theme;
        appearance.Mode = variant == Variant.Dark ? ThemeMode.Dark : ThemeMode.Light;
    }

    private static string Name(Variant variant) => variant == Variant.Dark ? "dark" : "light";

    private static void DeleteQuietly(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            DebugLog.Warn($"Snapshot: the throwaway folder {directory} could not be deleted", e);
        }
    }

    /// <summary>
    /// Runs everything the dispatcher has queued, down to the idle priorities (deferred bindings and template triggers,
    /// for one), as a window's first frames would.
    /// </summary>
    private static void Settle()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    /// <summary>The descendants of <paramref name="root"/> in the visual tree, depth first.</summary>
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// The layer the menus and the dialog are drawn in, over the window: as large as what it holds, so that the desk
    /// grows to show a menu that reaches past the window (as a popup would).
    /// </summary>
    private sealed class OverlayCanvas : Canvas
    {
        protected override Size MeasureOverride(Size constraint)
        {
            var size = default(Size);
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var left = GetLeft(child) is var x && double.IsNaN(x) ? 0 : x;
                var top = GetTop(child) is var y && double.IsNaN(y) ? 0 : y;
                size.Width = Math.Max(size.Width, left + child.DesiredSize.Width);
                size.Height = Math.Max(size.Height, top + child.DesiredSize.Height);
            }

            return size;
        }
    }

    /// <summary>
    /// A window's content (the main window's <see cref="MainView"/>, or the wizard's) in a window frame on a backdrop,
    /// laid out at one scale, as the root of its own visual tree, drawn with the resources <c>resources</c> builds.
    /// </summary>
    private sealed class Scene
    {
        private readonly Func<ResourceDictionary> _resources;
        private readonly double _scale;
        private readonly Border _desk;
        private readonly Border _window;
        private readonly Grid _frame;
        private readonly OverlayCanvas _overlay;
        private Border? _dialog;

        public Scene(FrameworkElement content, Func<ResourceDictionary> resources, double scale)
        {
            _resources = resources;
            _scale = scale;
            Content = content;

            var window = new Border { BorderThickness = new Thickness(1), MinWidth = MinWindowWidth, Child = content };
            _window = window;
            window.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Background);
            window.SetResourceReference(Border.BorderBrushProperty, ThemeKeys.WindowBorder);

            var shadow = new Border
            {
                CornerRadius = new CornerRadius(FrameRadius),
                Effect = new DropShadowEffect { Direction = 270, ShadowDepth = 8, BlurRadius = 36, Opacity = 0.25 },
            };
            shadow.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Background);

            _frame = new Grid { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            _frame.Children.Add(shadow);
            _frame.Children.Add(new RoundedClip { CornerRadius = new CornerRadius(FrameRadius), Child = window });

            _overlay = new OverlayCanvas();
            if (content is MainView main)
            {
                main.FlyoutLayer = _overlay;
            }

            var stage = new Grid();
            stage.Children.Add(_frame);
            stage.Children.Add(_overlay);

            _desk = new Border { Padding = new Thickness(DeskPadding), Child = stage, UseLayoutRounding = true };
        }

        /// <summary>What the window shows.</summary>
        public FrameworkElement Content { get; }

        /// <summary>The main window's content, in a scene of the main window.</summary>
        public MainView View => Content as MainView ?? throw new InvalidOperationException("Not a scene of the main window.");

        /// <summary>Bounds the window as a work area does (<c>null</c>: the default work area).</summary>
        public void Bound(Size? workArea)
        {
            _window.MaxWidth = workArea?.Width ?? double.PositiveInfinity;
            _window.MaxHeight = workArea?.Height ?? double.PositiveInfinity;
        }

        /// <summary>The view of the group named <paramref name="name"/> on the board.</summary>
        public GroupView Group(string name) =>
            Descendants(View).OfType<GroupView>().First(group => group.DataContext is GroupViewModel { Name: var n } && n == name);

        /// <summary>The view of the item shown as <paramref name="name"/> on the board.</summary>
        public ItemView Item(string name) =>
            Descendants(View).OfType<ItemView>().First(item => item.DataContext is ItemViewModel { DisplayName: var n } && n == name);

        /// <summary>The view of column <paramref name="index"/> of the board.</summary>
        public ColumnView Column(int index) => Descendants(View).OfType<ColumnView>().ElementAt(index);

        /// <summary>Shows <paramref name="prompt"/> centered on the window, as an owned dialog; <c>null</c> removes it.</summary>
        public void ShowDialog(PromptView? prompt)
        {
            if (_dialog is not null)
            {
                _overlay.Children.Remove(_dialog);
                _dialog = null;
            }

            if (prompt is null)
            {
                return;
            }

            var dialog = new Border { CornerRadius = new CornerRadius(FrameRadius), BorderThickness = new Thickness(1), Child = prompt };
            dialog.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Menu);
            dialog.SetResourceReference(Border.BorderBrushProperty, ThemeKeys.Border);
            dialog.SetResourceReference(UIElement.EffectProperty, ThemeKeys.FlyoutShadow);
            _dialog = dialog;
            _overlay.Children.Add(dialog);

            Layout();
            Canvas.SetLeft(dialog, Math.Round((_frame.ActualWidth - dialog.DesiredSize.Width) / 2));
            Canvas.SetTop(dialog, Math.Round((_frame.ActualHeight - dialog.DesiredSize.Height) / 2));
        }

        public void Layout()
        {
            for (var pass = 0; pass < MaxPasses; pass++)
            {
                var rescaled = EnsureScale();
                _desk.Measure(new Size(WorkArea.Width + (2 * DeskPadding), WorkArea.Height + (2 * DeskPadding)));
                _desk.Arrange(new Rect(_desk.DesiredSize));
                _desk.UpdateLayout();
                Settle();

                if (pass > 0 && !rescaled && !HasElementAtOtherScale(_desk))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// A tree outside any window does not hand its scale down to the elements it builds later (the containers of a
        /// list, a template's parts): they come at 96 DPI and are laid out, rounded and drawn so. When some are, the
        /// whole tree is given the scale again and laid out anew, as a window moved to another monitor would be. A tree
        /// already at its scale is left alone, so that nothing is laid out that the application would not lay out.
        /// </summary>
        private bool EnsureScale()
        {
            if (!HasElementAtOtherScale(_desk))
            {
                return false;
            }

            VisualTreeHelper.SetRootDpi(_desk, new DpiScale(_scale, _scale));
            InvalidateTree(_desk);
            return true;
        }

        private bool HasElementAtOtherScale(DependencyObject element)
        {
            if (element is Visual visual && VisualTreeHelper.GetDpi(visual).DpiScaleX != _scale)
            {
                return true;
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                if (HasElementAtOtherScale(VisualTreeHelper.GetChild(element, i)))
                {
                    return true;
                }
            }

            return false;
        }

        private static void InvalidateTree(DependencyObject element)
        {
            if (element is UIElement visual)
            {
                visual.InvalidateMeasure();
                visual.InvalidateVisual();
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                InvalidateTree(VisualTreeHelper.GetChild(element, i));
            }
        }

        public void Save(string path, Variant variant)
        {
            // The application tells its windows when its resources change, and this tree is in none: the same resources,
            // put at its root, make every DynamicResource in it look its value up again, as a window's tree does.
            _desk.Resources = _resources();

            // The mockups' backdrops: a light grey desk, a darker one under the dark variant.
            _desk.Background = ThemeApplier.SolidBrush(variant == Variant.Dark ? "#9aa0ab" : "#d6d9e0");
            Layout();

            var size = _desk.RenderSize;
            var bitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(size.Width * _scale),
                (int)Math.Ceiling(size.Height * _scale),
                96 * _scale,
                96 * _scale,
                PixelFormats.Pbgra32);
            bitmap.Render(_desk);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(path);
            encoder.Save(file);
        }
    }
}
