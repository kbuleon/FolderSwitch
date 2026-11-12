// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.ComponentModel;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Theming;
using FolderSwitch.Presentation.Tests.Support;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.Presentation.Tests;

public class AppearanceViewModelTests
{
    private readonly AppConfig _config = new();
    private readonly FakeSystemInfo _system = new();
    private readonly FakeConfigPersistence _persistence = new();

    private AppearanceViewModel Create() => new(_config, _system, _persistence);

    private static List<AppearanceChangedEventArgs> RecordAppearance(AppearanceViewModel vm)
    {
        var events = new List<AppearanceChangedEventArgs>();
        vm.AppearanceChanged += (_, e) => events.Add(e);
        return events;
    }

    private static List<string?> RecordChanges(INotifyPropertyChanged source)
    {
        var names = new List<string?>();
        source.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    // ----- Mode and variant -----

    [Fact]
    public void Auto_mode_follows_system_and_updates_live()
    {
        using var vm = Create();
        vm.Mode = ThemeMode.Auto;
        Assert.Equal(Variant.Light, vm.EffectiveVariant);

        var events = RecordAppearance(vm);
        var changes = RecordChanges(vm);
        _system.SetDark(true);

        Assert.Equal(Variant.Dark, vm.EffectiveVariant);
        Assert.Contains(nameof(AppearanceViewModel.EffectiveVariant), changes);
        var changed = Assert.Single(events);
        Assert.Equal(ThemeCatalog.Resolve("fluent", Variant.Dark, null), changed.Tokens);
        Assert.Equal(10, changed.RadiusPx);
        Assert.Equal(changed.Tokens, vm.Tokens);

        _system.SetDark(false);

        Assert.Equal(Variant.Light, vm.EffectiveVariant);
        Assert.Equal(2, events.Count);
        Assert.Equal(ThemeCatalog.Resolve("fluent", Variant.Light, null), events[1].Tokens);

        // Following the system is not an edit of the configuration.
        Assert.Equal(1, _persistence.SaveCount);
        Assert.Equal(ThemeMode.Auto, _config.Appearance.Mode);
    }

    [Fact]
    public void Auto_mode_starts_from_the_current_system_theme()
    {
        _config.Appearance.Mode = ThemeMode.Auto;
        _system.SetDark(true);

        using var vm = Create();

        Assert.Equal(Variant.Dark, vm.EffectiveVariant);
        Assert.Equal(ThemeCatalog.Resolve("fluent", Variant.Dark, null), vm.Tokens);
    }

    [Fact]
    public void Fixed_modes_ignore_the_system_theme()
    {
        using var vm = Create();
        var events = RecordAppearance(vm);

        _system.SetDark(true);
        Assert.Equal(Variant.Light, vm.EffectiveVariant);

        vm.Mode = ThemeMode.Dark;
        _system.SetDark(false);
        Assert.Equal(Variant.Dark, vm.EffectiveVariant);

        // Only the switch to Dark changed what is drawn.
        Assert.Single(events);
    }

    [Fact]
    public void Switching_to_auto_with_the_same_variant_saves_but_changes_nothing_on_screen()
    {
        using var vm = Create();
        var events = RecordAppearance(vm);

        vm.Mode = ThemeMode.Auto;

        Assert.Equal(ThemeMode.Auto, _config.Appearance.Mode);
        Assert.Equal(1, _persistence.SaveCount);
        Assert.Empty(events);
    }

    [Fact]
    public void Dispose_stops_following_the_system()
    {
        var vm = Create();
        vm.Mode = ThemeMode.Auto;
        Assert.Equal(1, _system.ThemeChangedSubscribers);

        vm.Dispose();

        Assert.Equal(0, _system.ThemeChangedSubscribers);
    }

    // ----- Backgrounds -----

    [Fact]
    public void Background_is_remembered_per_theme_and_variant()
    {
        using var vm = Create();

        vm.Background = "#ffffff";
        vm.Mode = ThemeMode.Dark;
        Assert.Equal("#1c1c1c", vm.Background); // fluent dark default: nothing chosen yet
        vm.Background = "#121a2b";

        vm.Mode = ThemeMode.Light;
        Assert.Equal("#ffffff", vm.Background);
        vm.Mode = ThemeMode.Dark;
        Assert.Equal("#121a2b", vm.Background);

        Assert.Equal("#ffffff", _config.Appearance.Backgrounds["fluent:light"]);
        Assert.Equal("#121a2b", _config.Appearance.Backgrounds["fluent:dark"]);

        // Another theme has its own memory.
        vm.Theme = "cards";
        Assert.Equal("#16171c", vm.Background);
        Assert.Equal("#16171c", vm.Tokens.Background);
        vm.Theme = "fluent";
        Assert.Equal("#121a2b", vm.Tokens.Background);
    }

    [Fact]
    public void Background_choices_start_with_theme_default_without_duplicates()
    {
        using var vm = Create();

        // Fluent light: its cream default is also a catalog tint, listed once, first.
        var light = vm.BackgroundChoices;
        Assert.Equal(new NamedColor(AppearanceViewModel.DefaultBackgroundKey, "#f7f5f0"), light[0]);
        Assert.Equal(
            ThemeCatalog.Backgrounds(Variant.Light).Where(c => c.Hex != "#f7f5f0"),
            light.Skip(1));
        Assert.Equal(light.Count, light.Select(c => c.Hex).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var changes = RecordChanges(vm);
        vm.Mode = ThemeMode.Dark;

        Assert.Contains(nameof(AppearanceViewModel.BackgroundChoices), changes);
        var dark = vm.BackgroundChoices;
        Assert.Equal(new NamedColor(AppearanceViewModel.DefaultBackgroundKey, "#1c1c1c"), dark[0]);
        Assert.DoesNotContain(dark.Skip(1), c => c.Key == "anthracite");
        Assert.Equal(ThemeCatalog.Backgrounds(Variant.Dark).Count, dark.Count);

        // A default that is not in the catalog adds an entry.
        vm.Theme = "pastel";
        Assert.Equal("#15161a", vm.BackgroundChoices[0].Hex);
        Assert.Equal(ThemeCatalog.Backgrounds(Variant.Dark).Count + 1, vm.BackgroundChoices.Count);
    }

    [Fact]
    public void Choosing_the_theme_default_removes_the_remembered_background()
    {
        using var vm = Create();
        vm.Background = "#ffffff";
        Assert.True(_config.Appearance.Backgrounds.ContainsKey("fluent:light"));

        vm.Background = "#F7F5F0";

        Assert.False(_config.Appearance.Backgrounds.ContainsKey("fluent:light"));
        Assert.Equal("#f7f5f0", vm.Background);
        Assert.Equal(2, _persistence.SaveCount);

        // Choosing the default again changes nothing.
        vm.Background = "#f7f5f0";
        Assert.Equal(2, _persistence.SaveCount);
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("#12345")]
    [InlineData("#12345g")]
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_custom_color_is_ignored(string? color)
    {
        using var vm = Create();
        vm.Background = "#ffffff";
        var events = RecordAppearance(vm);

        vm.Background = color!;

        Assert.Equal("#ffffff", vm.Background);
        Assert.Equal("#ffffff", _config.Appearance.Backgrounds["fluent:light"]);
        Assert.Equal(1, _persistence.SaveCount);
        Assert.Empty(events);
    }

    [Fact]
    public void Custom_color_is_stored_as_lowercase_rrggbb()
    {
        using var vm = Create();
        var events = RecordAppearance(vm);

        vm.Background = "#AbCdEf";
        Assert.Equal("#abcdef", _config.Appearance.Backgrounds["fluent:light"]);
        Assert.Equal("#abcdef", vm.Background);
        Assert.Equal(ThemeCatalog.Resolve("fluent", Variant.Light, "#abcdef"), Assert.Single(events).Tokens);

        vm.Background = "#80123456"; // alpha first, WPF order: dropped
        Assert.Equal("#123456", _config.Appearance.Backgrounds["fluent:light"]);
    }

    [Fact]
    public void Malformed_stored_background_reads_as_the_theme_default()
    {
        _config.Appearance.Backgrounds["fluent:light"] = "not a color";

        using var vm = Create();

        Assert.Equal("#f7f5f0", vm.Background);
        Assert.Equal("#f7f5f0", vm.Tokens.Background);
    }

    // ----- Theme, radius, tokens -----

    [Fact]
    public void Every_change_saves_once_and_raises_AppearanceChanged()
    {
        using var vm = Create();
        var events = RecordAppearance(vm);

        vm.Theme = "neon";
        Assert.Equal("neon", _config.Appearance.Theme);
        Assert.Equal(1, _persistence.SaveCount);
        Assert.Equal(ThemeCatalog.Resolve("neon", Variant.Light, null), Assert.Single(events).Tokens);

        vm.Radius = GroupRadius.Square;
        Assert.Equal(GroupRadius.Square, _config.Appearance.Radius);
        Assert.Equal(2, _persistence.SaveCount);
        Assert.Equal(0, events[1].RadiusPx);
        Assert.Equal(0, vm.RadiusPx);

        vm.Mode = ThemeMode.Dark;
        Assert.Equal(ThemeMode.Dark, _config.Appearance.Mode);
        Assert.Equal(3, _persistence.SaveCount);
        Assert.Equal(ThemeCatalog.Resolve("neon", Variant.Dark, null), events[2].Tokens);

        // The same values again: nothing.
        vm.Theme = "neon";
        vm.Radius = GroupRadius.Square;
        vm.Mode = ThemeMode.Dark;
        Assert.Equal(3, _persistence.SaveCount);
        Assert.Equal(3, events.Count);
    }

    [Fact]
    public void Theme_radius_follows_the_theme_while_radius_is_theme()
    {
        using var vm = Create();
        var events = RecordAppearance(vm);

        vm.Theme = "paper";

        Assert.Equal(4, vm.RadiusPx);
        Assert.Equal(4, Assert.Single(events).RadiusPx);
    }

    [Fact]
    public void Unknown_values_are_ignored()
    {
        using var vm = Create();

        vm.Theme = "nope";
        vm.Theme = null!;
        vm.Mode = (ThemeMode)42;
        vm.Radius = (GroupRadius)42;

        Assert.Equal("fluent", vm.Theme);
        Assert.Equal(ThemeMode.Light, vm.Mode);
        Assert.Equal(GroupRadius.Theme, vm.Radius);
        Assert.Equal(0, _persistence.SaveCount);
    }

    [Fact]
    public void Unknown_stored_theme_is_shown_as_fluent()
    {
        _config.Appearance.Theme = "hand-edited";

        using var vm = Create();

        Assert.Equal("fluent", vm.Theme);
        Assert.Equal(ThemeCatalog.Resolve("fluent", Variant.Light, null), vm.Tokens);
    }

    [Fact]
    public void Initial_tokens_reflect_the_configuration()
    {
        _config.Appearance.Mode = ThemeMode.Dark;
        _config.Appearance.Theme = "outline";
        _config.Appearance.Radius = GroupRadius.Strong;
        _config.Appearance.Backgrounds["outline:dark"] = "#1b2130";

        using var vm = Create();

        Assert.Equal(ThemeMode.Dark, vm.Mode);
        Assert.Equal("outline", vm.Theme);
        Assert.Equal(GroupRadius.Strong, vm.Radius);
        Assert.Equal("#1b2130", vm.Background);
        Assert.Equal(ThemeCatalog.Resolve("outline", Variant.Dark, "#1b2130"), vm.Tokens);
        Assert.Equal(18, vm.RadiusPx);
    }

    [Fact]
    public void Reload_edits_the_new_configuration_from_then_on()
    {
        using var vm = Create();
        var events = RecordAppearance(vm);
        var imported = new AppConfig();
        imported.Appearance.Theme = "compact";
        imported.Appearance.Mode = ThemeMode.Dark;

        vm.Reload(imported);

        Assert.Equal("compact", vm.Theme);
        Assert.Equal(Variant.Dark, vm.EffectiveVariant);
        Assert.Equal(ThemeCatalog.Resolve("compact", Variant.Dark, null), Assert.Single(events).Tokens);
        Assert.Equal(0, _persistence.SaveCount);

        vm.Radius = GroupRadius.Medium;

        Assert.Equal(GroupRadius.Medium, imported.Appearance.Radius);
        Assert.Equal(GroupRadius.Theme, _config.Appearance.Radius);
        Assert.Same(imported, _persistence.Saved.Single());
    }

    [Fact]
    public void DefaultColumnWidth_rounds_clamps_and_saves()
    {
        _config.Appearance.DefaultColumnWidth = 300;
        using var vm = Create();

        vm.DefaultColumnWidth = 223;

        Assert.Equal(220, vm.DefaultColumnWidth);
        Assert.Equal(1, _persistence.SaveCount);

        vm.DefaultColumnWidth = 9999;

        Assert.Equal(520, _config.Appearance.DefaultColumnWidth);
        Assert.Equal(2, _persistence.SaveCount);

        vm.DefaultColumnWidth = 520;
        vm.DefaultColumnWidth = 9999;

        Assert.Equal(2, _persistence.SaveCount);
    }

    [Fact]
    public void Reload_raises_DefaultColumnWidth()
    {
        using var vm = Create();
        var changes = RecordChanges(vm);
        var imported = new AppConfig();
        imported.Appearance.DefaultColumnWidth = 300;

        vm.Reload(imported);

        Assert.Contains(nameof(AppearanceViewModel.DefaultColumnWidth), changes);
        Assert.Equal(300, vm.DefaultColumnWidth);
    }
}
