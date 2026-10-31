using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using FolderSwitch.App.Themes;
using FolderSwitch.Core.Theming;
using FolderSwitch.Presentation.ViewModels;
using HotkeyModel = FolderSwitch.Core.Model.Hotkey;
using ThemeMode = FolderSwitch.Core.Model.ThemeMode;

namespace FolderSwitch.App.Views;

/// <summary>A step of the wizard's progress bar: « 1. Language », reached (done or shown) or not, shown or not.</summary>
public sealed record WizardStepHeader(string Label, bool IsReached, bool IsCurrent);

/// <summary>
/// A language of the wizard's first step: its own name, followed for the language of Windows by « (Windows language) »
/// in the language shown, which <see cref="Label"/> follows.
/// </summary>
public sealed class LanguageChoice : INotifyPropertyChanged
{
    internal LanguageChoice(string code, string nativeName, bool isWindowsLanguage)
    {
        Code = code;
        NativeName = nativeName;
        IsWindowsLanguage = isWindowsLanguage;
        Label = nativeName;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Code { get; }

    public string NativeName { get; }

    public bool IsWindowsLanguage { get; }

    /// <summary>What the list shows: « Français (langue de Windows) », or the name alone.</summary>
    public string Label { get; private set; }

    internal void UpdateLabel(string windowsMark)
    {
        var label = IsWindowsLanguage ? $"{NativeName} ({windowsMark})" : NativeName;
        if (!string.Equals(label, Label, StringComparison.Ordinal))
        {
            Label = label;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
        }
    }
}

/// <summary>
/// The content of the first-run wizard's window, bound to <see cref="WizardViewModel"/> (see <see cref="Attach"/>).
/// What the view model does not say in words is built here, in the current language, and again whenever the language,
/// the step, the mode, the theme or the shortcut changes: the progress bar, « Step n of 4 », the label of the forward
/// button, the modes and the theme tiles (drawn in the variant of the look in effect), and the shortcut's note.
/// </summary>
public partial class WizardView : UserControl, IDisposable
{
    public static readonly DependencyProperty TextsProperty = DependencyProperty.Register(
        nameof(Texts), typeof(ViewTexts), typeof(WizardView));

    public static readonly DependencyProperty AppearanceProperty = DependencyProperty.Register(
        nameof(Appearance), typeof(object), typeof(WizardView), new PropertyMetadata(null, OnAppearanceChanged));

    private static readonly DependencyPropertyKey StepHeadersPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(StepHeaders), typeof(IReadOnlyList<WizardStepHeader>), typeof(WizardView), new PropertyMetadata(null));

    public static readonly DependencyProperty StepHeadersProperty = StepHeadersPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey StepCountTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(StepCountText), typeof(string), typeof(WizardView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StepCountTextProperty = StepCountTextPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey ForwardLabelPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(ForwardLabel), typeof(string), typeof(WizardView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ForwardLabelProperty = ForwardLabelPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey LanguageChoicesPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(LanguageChoices), typeof(IReadOnlyList<LanguageChoice>), typeof(WizardView), new PropertyMetadata(null));

    public static readonly DependencyProperty LanguageChoicesProperty = LanguageChoicesPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey ModesPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Modes), typeof(IReadOnlyList<ModeChoice>), typeof(WizardView), new PropertyMetadata(null));

    public static readonly DependencyProperty ModesProperty = ModesPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey ThemesPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Themes), typeof(IReadOnlyList<ThemeTile>), typeof(WizardView), new PropertyMetadata(null));

    public static readonly DependencyProperty ThemesProperty = ThemesPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey PreviewVariantPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(PreviewVariant), typeof(Variant), typeof(WizardView), new PropertyMetadata(Variant.Light));

    public static readonly DependencyProperty PreviewVariantProperty = PreviewVariantPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey HotkeyNotePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HotkeyNote), typeof(HotkeyNote), typeof(WizardView), new PropertyMetadata(null));

    public static readonly DependencyProperty HotkeyNoteProperty = HotkeyNotePropertyKey.DependencyProperty;

    private static readonly string[] StepKeys = ["Wizard.Step.Language", "Wizard.Step.Hotkey", "Wizard.Step.Appearance", "Wizard.Step.Startup"];

    private WizardViewModel? _viewModel;
    private bool _disposed;

    public WizardView()
    {
        // The variant the theme tiles are drawn in: the one of the look in effect, which follows the mode chosen.
        SetResourceReference(AppearanceProperty, ThemeKeys.Appearance);
        InitializeComponent();
        AppIcon.Source = AppIconDrawing.Glyph;
    }

    /// <summary>Raised by ✕.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The texts of the view itself, in the current language.</summary>
    public ViewTexts? Texts
    {
        get => (ViewTexts?)GetValue(TextsProperty);
        private set => SetValue(TextsProperty, value);
    }

    /// <summary>The look in effect (<see cref="AppearanceState"/>), from the resource <see cref="ThemeKeys.Appearance"/>.</summary>
    public object? Appearance
    {
        get => GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    public IReadOnlyList<WizardStepHeader>? StepHeaders => (IReadOnlyList<WizardStepHeader>?)GetValue(StepHeadersProperty);

    /// <summary>« Step 2 of 4 ».</summary>
    public string StepCountText => (string)GetValue(StepCountTextProperty);

    /// <summary>« Next → », or « Finish ✓ » at the last step.</summary>
    public string ForwardLabel => (string)GetValue(ForwardLabelProperty);

    public IReadOnlyList<LanguageChoice>? LanguageChoices => (IReadOnlyList<LanguageChoice>?)GetValue(LanguageChoicesProperty);

    public IReadOnlyList<ModeChoice>? Modes => (IReadOnlyList<ModeChoice>?)GetValue(ModesProperty);

    public IReadOnlyList<ThemeTile>? Themes => (IReadOnlyList<ThemeTile>?)GetValue(ThemesProperty);

    /// <summary>The variant the theme tiles are drawn in.</summary>
    public Variant PreviewVariant => (Variant)GetValue(PreviewVariantProperty);

    /// <summary>The note of the shortcut editor while the shortcut has no problem.</summary>
    public HotkeyNote? HotkeyNote => (HotkeyNote?)GetValue(HotkeyNoteProperty);

    /// <summary>The element that moves the window when dragged: the title row.</summary>
    internal UIElement DragArea => TitleBar;

    /// <summary>
    /// Shows <paramref name="viewModel"/>, once. <paramref name="windowsLanguage"/> is the code of the language Windows
    /// uses, when FolderSwitch has it (marked in the list); <c>null</c> otherwise.
    /// </summary>
    internal void Attach(WizardViewModel viewModel, ViewTexts texts, string? windowsLanguage)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(texts);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_viewModel is not null)
        {
            throw new InvalidOperationException("The wizard's view is already attached.");
        }

        _viewModel = viewModel;
        Texts = texts;

        // The list before the view model: its selection is the view model's language.
        SetValue(LanguageChoicesPropertyKey, viewModel.Languages
            .Select(language => new LanguageChoice(
                language.Code,
                language.NativeName,
                string.Equals(language.Code, windowsLanguage, StringComparison.OrdinalIgnoreCase)))
            .ToList()
            .AsReadOnly());

        texts.PropertyChanged += OnTextsChanged;
        viewModel.PropertyChanged += OnViewModelChanged;
        viewModel.Hotkey.PropertyChanged += OnHotkeyChanged;

        RefreshAll();
        DataContext = viewModel;
    }

    /// <summary>Lets go of the view model and of the texts (which live as long as the localizer).</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_viewModel is { } viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelChanged;
            viewModel.Hotkey.PropertyChanged -= OnHotkeyChanged;
        }

        if (Texts is { } texts)
        {
            texts.PropertyChanged -= OnTextsChanged;
        }

        // The editor follows the language through the texts: it lets go of them.
        Editor.Texts = null;
    }

    private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (WizardView)d;
        view.SetValue(PreviewVariantPropertyKey, e.NewValue is AppearanceState state ? state.Variant : Variant.Light);
    }

    private void RefreshAll()
    {
        RefreshLanguages();
        RefreshSteps();
        RefreshAppearance();
        RefreshHotkeyNote();
    }

    private string Text(string key) => Texts?[key] ?? key;

    private void RefreshLanguages()
    {
        var mark = Text("Wizard.Language.WindowsMark");
        foreach (var choice in LanguageChoices ?? [])
        {
            choice.UpdateLabel(mark);
        }
    }

    private void RefreshSteps()
    {
        if (_viewModel is not { } viewModel)
        {
            return;
        }

        var step = viewModel.Step;
        SetValue(StepHeadersPropertyKey, StepKeys
            .Select((key, index) => new WizardStepHeader(
                string.Format(CultureInfo.CurrentCulture, "{0}. {1}", index + 1, Text(key)),
                index <= step,
                index == step))
            .ToList()
            .AsReadOnly());
        SetValue(StepCountTextPropertyKey, string.Format(CultureInfo.CurrentCulture, Text("Wizard.StepCount"), step + 1, StepKeys.Length));
        SetValue(ForwardLabelPropertyKey, step == WizardViewModel.LastStep ? Text("Wizard.Finish") + " ✓" : Text("Wizard.Next") + " →");
    }

    private void RefreshAppearance()
    {
        if (_viewModel is not { } viewModel)
        {
            return;
        }

        SetValue(ModesPropertyKey, new List<ModeChoice>
        {
            new(ThemeMode.Light, "☀", Text("Appearance.Mode.Light"), viewModel.Mode == ThemeMode.Light),
            new(ThemeMode.Dark, "☾", Text("Appearance.Mode.Dark"), viewModel.Mode == ThemeMode.Dark),
            new(ThemeMode.Auto, "◐", Text("Wizard.Mode.Auto"), viewModel.Mode == ThemeMode.Auto),
        }.AsReadOnly());

        // Each tile on its theme's own background: the wizard does not choose one.
        SetValue(ThemesPropertyKey, ThemeCatalog.ThemeKeys
            .Select(key => new ThemeTile(key, Text("Theme." + key), null, key == viewModel.Theme))
            .ToList()
            .AsReadOnly());
    }

    /// <summary>
    /// With no problem, a note says what the shortcut is: Ctrl + ⊞ Win is the recommended one; modifier keys alone react
    /// when released; any other combination was found free.
    /// </summary>
    private void RefreshHotkeyNote()
    {
        if (_viewModel is not { } viewModel)
        {
            return;
        }

        var editor = viewModel.Hotkey;
        HotkeyNote? note = editor.Issues.Count > 0
            ? null
            : editor.Current == HotkeyModel.Default
                ? new HotkeyNote(Text("Hotkey.Note.Recommended"), IsConfirmation: false)
                : editor.Current.IsModifierOnly
                    ? new HotkeyNote(Text("Hotkey.Note.ModifiersOnly"), IsConfirmation: false)
                    : new HotkeyNote(Text("Hotkey.Note.Free"), IsConfirmation: true);
        SetValue(HotkeyNotePropertyKey, note);
    }

    private void OnTextsChanged(object? sender, PropertyChangedEventArgs e) => RefreshAll();

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(WizardViewModel.Step):
                RefreshSteps();
                break;
            case nameof(WizardViewModel.Mode):
            case nameof(WizardViewModel.Theme):
                RefreshAppearance();
                break;
            case null or "":
                RefreshAll();
                break;
        }
    }

    private void OnHotkeyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or "" or nameof(HotkeyEditorViewModel.Issues) or nameof(HotkeyEditorViewModel.Current))
        {
            RefreshHotkeyNote();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnModeClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is { } viewModel && sender is FrameworkElement { DataContext: ModeChoice choice })
        {
            viewModel.Mode = choice.Value;
        }
    }

    private void OnThemeClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is { } viewModel && sender is FrameworkElement { DataContext: ThemeTile tile })
        {
            viewModel.Theme = tile.Key;
        }
    }
}
