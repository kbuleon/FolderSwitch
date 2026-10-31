using FolderSwitch.Core.Localization;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>A language the user can choose, as listed by the localizer.</summary>
/// <param name="Code">The code, as spelled by its translation file (<c>fr</c>, <c>pt-BR</c>...).</param>
/// <param name="NativeName">The language's own name (<c>Français</c>), shown in the list whatever the current language.</param>
public sealed record LanguageOption(string Code, string NativeName)
{
    /// <summary>The languages of <paramref name="localizer"/>, in its order.</summary>
    internal static IReadOnlyList<LanguageOption> ListFrom(Localizer localizer) =>
        localizer.Available
            .Select(language => new LanguageOption(language.Code, language.NativeName))
            .ToList()
            .AsReadOnly();

    /// <summary>The option whose code is <paramref name="code"/>, ignoring case; <c>null</c> when there is none.</summary>
    internal static LanguageOption? Find(IReadOnlyList<LanguageOption> options, string? code) =>
        code is null
            ? null
            : options.FirstOrDefault(option => string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase));
}
