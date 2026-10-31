using System.Windows;
using System.Windows.Interop;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.App.Themes;
using FolderSwitch.Windows.SystemServices;

namespace FolderSwitch.App.Views;

/// <summary>
/// A message box of FolderSwitch's own, in the theme's colors (<see cref="PromptView"/>), in place of the system's.
/// Borderless like the main window: rounded and shadowed by the Desktop Window Manager on Windows 11, with a 1 px
/// border on Windows 10. Modal; owned by the main window when it is on screen (centered on it, and the focus goes back
/// to it afterwards), else centered on the screen and listed in the taskbar so that it can be found.
/// </summary>
internal sealed class PromptWindow : Window
{
    private readonly PromptView _view;
    private bool _answer;

    public PromptWindow(PromptView view, Window? owner)
    {
        ArgumentNullException.ThrowIfNull(view);

        _view = view;
        Title = "FolderSwitch";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        UseLayoutRounding = true;
        ShowInTaskbar = owner is null;
        if (owner is not null)
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        SetResourceReference(BackgroundProperty, ThemeKeys.Menu);
        SetResourceReference(BorderBrushProperty, ThemeKeys.WindowBorder);
        BorderThickness = WindowFrame.SupportsRoundedCorners ? new Thickness(0) : new Thickness(1);
        Content = view;

        view.Answered += OnAnswered;
        Loaded += (_, _) => view.DefaultButton.Focus();
    }

    /// <summary>Shows the box until it is answered: true for « Yes », false for anything else.</summary>
    public bool Ask()
    {
        ShowDialog();
        return _answer;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        if (WindowFrame.SupportsRoundedCorners && !WindowFrame.SetRoundedCorners(hwnd, small: false))
        {
            DebugLog.Warn("The corners of a message box could not be rounded");
        }

        WindowFrame.EnableShadow(hwnd);
    }

    protected override void OnClosed(EventArgs e)
    {
        _view.Answered -= OnAnswered;
        base.OnClosed(e);
    }

    private void OnAnswered(object? sender, bool yes)
    {
        _answer = yes;
        DialogResult = yes;
    }
}
