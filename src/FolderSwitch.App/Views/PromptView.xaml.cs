using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FolderSwitch.App.Themes;

namespace FolderSwitch.App.Views;

/// <summary>What a message box of FolderSwitch asks or says.</summary>
internal enum PromptKind
{
    /// <summary>A yes/no question (a deletion to confirm).</summary>
    Confirm,

    /// <summary>Something went wrong.</summary>
    Error,

    /// <summary>A notice (a configuration restored at startup).</summary>
    Info,

    /// <summary>The offer of an update: « Install » or « Later » (the notice icon; « Later » is the default answer).</summary>
    Update,
}

/// <summary>
/// The content of a message box (<see cref="PromptWindow"/>): its icon follows the kind (a question in amber, an
/// error in red, a notice in the menu's text color), its texts come from the localizer.
/// </summary>
public partial class PromptView : UserControl
{
    public static readonly DependencyProperty IconBackgroundProperty = DependencyProperty.Register(
        nameof(IconBackground), typeof(Brush), typeof(PromptView));

    public static readonly DependencyProperty IconForegroundProperty = DependencyProperty.Register(
        nameof(IconForeground), typeof(Brush), typeof(PromptView));

    /// <param name="yesText">The text of the affirmative button of a question; « Yes » when <c>null</c>.</param>
    /// <param name="noText">The text of the other button; « No » for a question, « OK » otherwise, when <c>null</c>.</param>
    internal PromptView(PromptKind kind, string message, Func<string, string> localize, string? yesText = null, string? noText = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(localize);

        Kind = kind;
        Message = message;
        (Glyph, var background, var foreground) = kind switch
        {
            PromptKind.Confirm => ("", ThemeKeys.WarningBackground, ThemeKeys.Warning),
            PromptKind.Error => ("", ThemeKeys.DangerBackground, ThemeKeys.Danger),
            _ => ("", ThemeKeys.SegmentTrack, ThemeKeys.MenuText),
        };
        var isQuestion = kind is PromptKind.Confirm or PromptKind.Update;
        YesText = yesText ?? localize("Dialog.Yes");
        NoText = noText ?? (isQuestion ? localize("Dialog.No") : localize("Dialog.OK"));
        SetResourceReference(IconBackgroundProperty, background);
        SetResourceReference(IconForegroundProperty, foreground);

        InitializeComponent();

        if (!isQuestion)
        {
            YesButton.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Raised with the answer: true for « Yes », false for « No », « OK », Escape or Enter.</summary>
    public event EventHandler<bool>? Answered;

    internal PromptKind Kind { get; }

    public string Message { get; }

    public string Glyph { get; }

    public string YesText { get; }

    /// <summary>« No » for a question, « OK » otherwise.</summary>
    public string NoText { get; }

    public Brush? IconBackground
    {
        get => (Brush?)GetValue(IconBackgroundProperty);
        set => SetValue(IconBackgroundProperty, value);
    }

    public Brush? IconForeground
    {
        get => (Brush?)GetValue(IconForegroundProperty);
        set => SetValue(IconForegroundProperty, value);
    }

    /// <summary>The button the keyboard starts on: the default answer.</summary>
    internal Button DefaultButton => NoButton;

    private void OnYesClick(object sender, RoutedEventArgs e) => Answered?.Invoke(this, true);

    private void OnNoClick(object sender, RoutedEventArgs e) => Answered?.Invoke(this, false);
}
