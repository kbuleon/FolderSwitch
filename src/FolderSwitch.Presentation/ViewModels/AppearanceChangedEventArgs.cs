using FolderSwitch.Core.Theming;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>What the window must now be drawn with: see <see cref="AppearanceViewModel.AppearanceChanged"/>.</summary>
public sealed class AppearanceChangedEventArgs : EventArgs
{
    public AppearanceChangedEventArgs(ThemeTokens tokens, int radiusPx)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        Tokens = tokens;
        RadiusPx = radiusPx;
    }

    /// <summary>The colors and metrics of the theme, with the chosen background applied.</summary>
    public ThemeTokens Tokens { get; }

    /// <summary>Corner radius of group blocks, in px.</summary>
    public int RadiusPx { get; }
}
