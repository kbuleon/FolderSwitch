using CommunityToolkit.Mvvm.ComponentModel;
using FolderSwitch.Core.Web;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>
/// The small window that asks for a web address. The field starts with the clipboard when it holds a web address;
/// what is typed is accepted when <see cref="WebAddress.TryNormalize"/> takes it, and <see cref="Result"/> then holds
/// the normalized address. Meant to be used from the UI thread.
/// </summary>
public sealed partial class UrlPromptViewModel : ObservableObject
{
    /// <param name="clipboardText">The text on the clipboard, <c>null</c> when none: it prefills the field only when it is a web address.</param>
    public UrlPromptViewModel(string? clipboardText)
    {
        Text = WebAddress.IsWebUrl(clipboardText) ? clipboardText!.Trim() : string.Empty;
    }

    /// <summary>What the field holds.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    [NotifyPropertyChangedFor(nameof(ShowsError))]
    public partial string Text { get; set; }

    /// <summary>True when <see cref="Text"/> is an address that can be added.</summary>
    public bool CanAdd => WebAddress.TryNormalize(Text, out _);

    /// <summary>True when something was typed that is not an address: the error is shown under the field.</summary>
    public bool ShowsError => !string.IsNullOrWhiteSpace(Text) && !CanAdd;

    /// <summary>The normalized address, set by <see cref="Accept"/>; <c>null</c> until then.</summary>
    public string? Result { get; private set; }

    /// <summary>Takes the text as the result; nothing when it is not an address.</summary>
    public void Accept()
    {
        if (WebAddress.TryNormalize(Text, out var url))
        {
            Result = url;
        }
    }
}
