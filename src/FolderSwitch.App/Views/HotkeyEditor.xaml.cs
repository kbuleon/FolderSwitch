using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// A note of the shortcut editor when the combination has no problem: advice in blue (Ctrl + ⊞ Win is the recommended
/// one), or a confirmation in green (<see cref="IsConfirmation"/>: the combination is free).
/// </summary>
public sealed record HotkeyNote(string Text, bool IsConfirmation);

/// <summary>
/// The shortcut editor, bound to a <see cref="HotkeyEditorViewModel"/>: in the settings panel and in the first-run
/// wizard. The capture, the checks and the test belong to the view model; this only shows them, with the
/// <see cref="Note"/> its owner may add.
/// </summary>
public partial class HotkeyEditor : UserControl
{
    public static readonly DependencyProperty TextsProperty = DependencyProperty.Register(
        nameof(Texts), typeof(ViewTexts), typeof(HotkeyEditor), new PropertyMetadata(null, OnTextsChanged));

    public static readonly DependencyProperty NoteProperty = DependencyProperty.Register(
        nameof(Note), typeof(HotkeyNote), typeof(HotkeyEditor), new PropertyMetadata(null));

    private static readonly DependencyPropertyKey RecognizedPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Recognized), typeof(string), typeof(HotkeyEditor), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty RecognizedProperty = RecognizedPropertyKey.DependencyProperty;

    public HotkeyEditor()
    {
        InitializeComponent();
    }

    /// <summary>The texts of the view itself (the buttons, the capture's invitation).</summary>
    public ViewTexts? Texts
    {
        get => (ViewTexts?)GetValue(TextsProperty);
        set => SetValue(TextsProperty, value);
    }

    /// <summary>Shown under the problems, as a note (<c>null</c>: nothing); the wizard's, not the settings'.</summary>
    public HotkeyNote? Note
    {
        get => (HotkeyNote?)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    /// <summary>What the test zone says once the shortcut fired: « ✓ Shortcut recognized ».</summary>
    public string Recognized => (string)GetValue(RecognizedProperty);

    private static void OnTextsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var editor = (HotkeyEditor)d;
        if (e.OldValue is ViewTexts old)
        {
            old.PropertyChanged -= editor.OnLanguageChanged;
        }

        if (e.NewValue is ViewTexts texts)
        {
            texts.PropertyChanged += editor.OnLanguageChanged;
        }

        editor.UpdateRecognized();
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => UpdateRecognized();

    private void UpdateRecognized() =>
        SetValue(RecognizedPropertyKey, Texts is { } texts ? "✓ " + texts["Hotkey.Recognized"] : string.Empty);
}
