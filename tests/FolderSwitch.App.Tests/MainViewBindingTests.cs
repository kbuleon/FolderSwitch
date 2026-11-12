// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using FolderSwitch.App.Views;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;
using FolderSwitch.Presentation.Services;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Tests;

/// <summary>
/// The title bar's bindings to <see cref="MainView.Appearance"/>: assigned after the view is built, as the window does,
/// the width in the « Equalize widths » tooltip must still be read. Built on an STA thread, without a window.
/// </summary>
public sealed class MainViewBindingTests
{
    [Fact]
    public void The_equalize_tooltip_shows_the_default_width_of_an_appearance_assigned_after_construction() => OnSta(() =>
    {
        var application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/FolderSwitch;component/Themes/Generic.xaml", UriKind.Relative),
        });

        var localizer = Localizer.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Strings"));
        localizer.SetLanguage("en");
        var config = new AppConfig();
        config.Appearance.DefaultColumnWidth = 300;
        using var appearance = new AppearanceViewModel(config, new FakeSystem(), new FakePersistence());

        var view = new MainView();
        view.Texts = new ViewTexts(localizer);
        view.Appearance = appearance;

        var button = Descendants(view).OfType<Button>().Single(b => b.ToolTip is string tip && tip.Contains("300px"));
        Assert.Contains("300px", AutomationProperties.GetName(button));

        appearance.DefaultColumnWidth = 340;
        Assert.Contains("340px", Assert.IsType<string>(button.ToolTip));
    });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var inner in Descendants(child))
            {
                yield return inner;
            }
        }
    }

    private sealed class FakeSystem : ISystemInfo
    {
        public event EventHandler? ThemeChanged
        {
            add { }
            remove { }
        }

        public WindowsGeneration Generation => WindowsGeneration.Windows11;

        public string UiCultureName => "en-US";

        public bool AppsUseDarkTheme => false;
    }

    private sealed class FakePersistence : IConfigPersistence
    {
        public void Save(AppConfig c)
        {
        }
    }

    private static void OnSta(Action test)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                test();
            }
            catch (Exception e)
            {
                failure = ExceptionDispatchInfo.Capture(e);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
