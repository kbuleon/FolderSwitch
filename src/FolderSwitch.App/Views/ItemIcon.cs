// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.Core.Services;

namespace FolderSwitch.App.Views;

/// <summary>
/// The pictures of the items that do not come from the application: the icon Windows gives a file type
/// (<see cref="IFileTypeIcons"/>) and the icon a site gave itself (its favicon, a file of the cache). Both are made off
/// the UI thread, frozen, and kept for the life of the process: one per extension, one per favicon file. A picture
/// that cannot be made is <c>null</c>, kept as well: the item keeps its drawn icon.
/// </summary>
/// <remarks>
/// <para>
/// UI thread only; the tasks it hands out complete on the thread pool and never fault.
/// </para>
/// <para>
/// The shell is asked for one extension at a time: <see cref="IFileTypeIcons"/> waits a bounded time for each answer,
/// the wait in its queue included, and keeps a timeout as its answer, so that asking for many at once could make a slow
/// one fail for the whole session.
/// </para>
/// <para>
/// A favicon that cannot be decoded stays undecoded for the session: the cache writes its files under a temporary
/// name and moves them into place, so a failure comes from the file itself, never from a file read half-written, and
/// trying it again would fail again.
/// </para>
/// </remarks>
internal sealed class ItemIcons
{
    /// <summary>
    /// The width a favicon is decoded at: twice its 16-DIP box, so that it stays sharp up to a 200 % scale (and is
    /// scaled down smoothly below it).
    /// </summary>
    private const int FaviconPixels = 32;

    private readonly IFileTypeIcons? _fileTypes;
    private readonly SemaphoreSlim _shell = new(1, 1);
    private readonly Dictionary<string, Task<ImageSource?>> _types = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<ImageSource?>> _favicons = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="fileTypes">Where the icons of the file types come from; <c>null</c>: every file keeps its drawn icon.</param>
    public ItemIcons(IFileTypeIcons? fileTypes) => _fileTypes = fileTypes;

    /// <summary>The icon of the files of <paramref name="extension"/> (<c>.xlsx</c>, with its dot); <c>null</c> when Windows gives none.</summary>
    public Task<ImageSource?> TypeIconAsync(string extension)
    {
        if (_fileTypes is not { } fileTypes)
        {
            return Task.FromResult<ImageSource?>(null);
        }

        if (!_types.TryGetValue(extension, out var task))
        {
            task = Task.Run(() => ReadTypeIconAsync(fileTypes, extension));
            _types.Add(extension, task);
        }

        return task;
    }

    /// <summary>The favicon kept in the file <paramref name="path"/>; <c>null</c> when it cannot be read or decoded.</summary>
    public Task<ImageSource?> FaviconAsync(string path)
    {
        if (!_favicons.TryGetValue(path, out var task))
        {
            task = Task.Run(() => DecodeFavicon(path));
            _favicons.Add(path, task);
        }

        return task;
    }

    /// <summary>The snapshots: waits for every picture asked for so far, at most <paramref name="timeout"/>.</summary>
    internal void WaitForPending(TimeSpan timeout) =>
        Task.WhenAll(_types.Values.Concat(_favicons.Values)).Wait(timeout);

    private async Task<ImageSource?> ReadTypeIconAsync(IFileTypeIcons fileTypes, string extension)
    {
        await _shell.WaitAsync().ConfigureAwait(false);
        try
        {
            return ToBitmap(fileTypes.SmallIconFor(extension));
        }
        catch (Exception e) when (!ExceptionPolicy.IsCritical(e))
        {
            DebugLog.Warn($"The icon of the {extension} files could not be made", e);
            return null;
        }
        finally
        {
            _shell.Release();
        }
    }

    /// <summary>
    /// The pixels of a shell icon as a bitmap. They are straight (not premultiplied) BGRA, as an icon's color bitmap
    /// holds them: drawn as premultiplied, the half-transparent pixels of the edges would come out too light.
    /// </summary>
    private static ImageSource? ToBitmap(IconPixels? pixels)
    {
        if (pixels is not { Width: > 0, Height: > 0 } || pixels.Bgra32.Length < pixels.Width * pixels.Height * 4)
        {
            return null;
        }

        var bitmap = BitmapSource.Create(pixels.Width, pixels.Height, 96, 96, PixelFormats.Bgra32, null, pixels.Bgra32, pixels.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static ImageSource? DecodeFavicon(string path)
    {
        try
        {
            // Read whole first: the file is never held open (the cache may replace it), and the decoder works in memory.
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = FaviconPixels;
            image.StreamSource = new MemoryStream(File.ReadAllBytes(path), writable: false);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception e) when (!ExceptionPolicy.IsCritical(e))
        {
            DebugLog.Info($"The favicon {Path.GetFileName(path)} could not be shown: {e.Message}");
            return null;
        }
    }
}

/// <summary>
/// The picture of an item that is not drawn by the application (<see cref="ItemIcons"/>, found through the
/// <see cref="MainView"/> it is in): a file's type icon (<see cref="Extension"/>), a site's favicon
/// (<see cref="FaviconPath"/>, first when both are set). Empty, <see cref="HasPicture"/> false, for anything else, until
/// the picture arrives, and when there is none: the item's drawn icon shows in its place (see <see cref="ItemView"/>).
/// </summary>
public sealed class ItemIcon : Image
{
    public static readonly DependencyProperty ExtensionProperty = DependencyProperty.Register(
        nameof(Extension), typeof(string), typeof(ItemIcon), new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty FaviconPathProperty = DependencyProperty.Register(
        nameof(FaviconPath), typeof(string), typeof(ItemIcon), new PropertyMetadata(null, OnSourceChanged));

    private static readonly DependencyPropertyKey HasPicturePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasPicture), typeof(bool), typeof(ItemIcon), new PropertyMetadata(false));

    public static readonly DependencyProperty HasPictureProperty = HasPicturePropertyKey.DependencyProperty;

    /// <summary>Tells a picture that arrives for an older request from the current one.</summary>
    private int _request;

    public ItemIcon()
    {
        Stretch = Stretch.Uniform;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        Loaded += (_, _) => Refresh();
    }

    /// <summary>The extension of a file, with its dot; <c>null</c> for anything else.</summary>
    public string? Extension
    {
        get => (string?)GetValue(ExtensionProperty);
        set => SetValue(ExtensionProperty, value);
    }

    /// <summary>The file of a site's favicon in the cache; <c>null</c> when there is none.</summary>
    public string? FaviconPath
    {
        get => (string?)GetValue(FaviconPathProperty);
        set => SetValue(FaviconPathProperty, value);
    }

    /// <summary>True while a picture is shown.</summary>
    public bool HasPicture
    {
        get => (bool)GetValue(HasPictureProperty);
        private set => SetValue(HasPicturePropertyKey, value);
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ItemIcon)d).Refresh();

    private void Refresh()
    {
        var request = ++_request;
        var icons = MainView.Of(this)?.Icons;
        var task = icons is null ? null
            : FaviconPath is { Length: > 0 } path ? icons.FaviconAsync(path)
            : Extension is { Length: > 0 } extension ? icons.TypeIconAsync(extension)
            : null;

        if (task is null || task.IsCompleted)
        {
            Show(task?.Result);
            return;
        }

        // The drawn icon meanwhile; the picture once it is made, unless another one was asked for since.
        Show(null);
        task.ContinueWith(
            done => Dispatcher.BeginInvoke(() =>
            {
                if (request == _request)
                {
                    Show(done.Result);
                }
            }),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void Show(ImageSource? picture)
    {
        Source = picture;
        HasPicture = picture is not null;
    }
}
