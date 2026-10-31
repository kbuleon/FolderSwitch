using System.ComponentModel;
using FolderSwitch.Core.Localization;

namespace FolderSwitch.App.Views;

/// <summary>
/// The localizer as the views bind to it: <c>{Binding Texts[Window.Close]}</c>. Every such binding follows a change of
/// language. For the texts of the views themselves (tooltips, labels no view model carries); a view model's own texts
/// come from the view model.
/// </summary>
/// <remarks>UI thread only, like the localizer. Lives as long as the localizer.</remarks>
public sealed class ViewTexts : INotifyPropertyChanged
{
    /// <summary>The name WPF gives an indexer in change notifications.</summary>
    private const string IndexerName = "Item[]";

    private readonly Localizer _localizer;

    public ViewTexts(Localizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        _localizer = localizer;
        _localizer.LanguageChanged += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(IndexerName));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The text of <paramref name="key"/> in the current language (the key itself when it has none).</summary>
    public string this[string key] => _localizer[key];
}
