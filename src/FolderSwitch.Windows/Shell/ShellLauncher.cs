// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using FolderSwitch.Core.Services;
using FolderSwitch.Core.Web;

namespace FolderSwitch.Windows.Shell;

/// <summary>
/// Opens files and web addresses as a double-click in Explorer does: <c>ShellExecuteEx</c> with the default verb, the shell
/// choosing the associated application.
/// </summary>
/// <remarks>
/// <para>
/// <b>Threads.</b> Any thread. <c>ShellExecuteEx</c> may hand the launch to shell extensions that need
/// a single-threaded apartment: on an STA thread (the UI thread) it runs there, otherwise on a
/// short-lived STA thread that the caller waits for. <c>SEE_MASK_NOASYNC</c> makes the launch complete
/// before the call returns, since neither the caller nor that thread pumps messages for it afterwards.
/// </para>
/// <para>
/// <b>Shell UI.</b> The shell's own dialogs are kept, as for a double-click: "How do you want to open
/// this file?" for a file with no associated application, the security warning for a downloaded file,
/// the elevation prompt for a program that requires it, and its error message when the launch fails
/// (shown before the call returns, which then returns <c>false</c>: the user has been told). The user
/// declining one of those prompts (<c>ERROR_CANCELLED</c>) is not a failure.
/// </para>
/// <para>
/// <b>Working directory.</b> The file's folder, as for a double-click in Explorer: a batch file or a
/// program that uses relative paths finds its neighbours.
/// </para>
/// </remarks>
public sealed class ShellLauncher : IShellLauncher
{
    private const string ThreadName = "FolderSwitch shell execute";

    /// <summary><c>ERROR_CANCELLED</c>: the user declined a prompt the shell showed.</summary>
    private const int ErrorCancelled = 1223;

    /// <inheritdoc/>
    /// <returns>
    /// <c>true</c> once the file is opened (or the user declined a prompt of the shell); <c>false</c> when
    /// <c>ShellExecuteEx</c> failed: the launch runs with the shell's UI, so the shell has shown its own
    /// message by then (« Windows cannot find… »), and the caller has nothing more to say.
    /// </returns>
    public bool OpenFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            Execute(path, verb: null, showErrors: true, WorkingDirectory(path));
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    /// <returns>
    /// <c>false</c> without launching anything when <paramref name="url"/> is not an <c>http</c>, <c>https</c>,
    /// <c>ftp</c> or <c>file</c> address (<c>javascript:</c>, <c>mailto:</c>, any other scheme, a bare path: the shell
    /// would run them). A <c>file</c> address is as trusted as a File item, which the shell opens too; otherwise as
    /// <see cref="OpenFile"/>.
    /// </returns>
    public bool OpenUrl(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        if (!WebAddress.IsSiteUrl(url))
        {
            return false;
        }

        try
        {
            Execute(url, verb: null, showErrors: true);
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// <c>ShellExecuteEx</c> of <paramref name="target"/> (a path, <c>shell:</c> or <c>::{CLSID}</c>
    /// name, passed as <c>lpFile</c>, never on a command line) with <paramref name="verb"/>
    /// (<c>null</c>: the default verb), on an STA thread, in <paramref name="directory"/> (<c>null</c>:
    /// the caller's current directory). <paramref name="showErrors"/> <c>false</c>: no error message
    /// box (<c>SEE_MASK_FLAG_NO_UI</c>).
    /// </summary>
    /// <exception cref="Win32Exception">The launch failed (see <see cref="IsDeclinedByUser"/> for what is not a failure).</exception>
    internal static void Execute(string target, string? verb, bool showErrors, string? directory = null)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            ExecuteHere(target, verb, showErrors, directory);
            return;
        }

        ExceptionDispatchInfo? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                ExecuteHere(target, verb, showErrors, directory);
            }
            catch (Exception e)
            {
                error = ExceptionDispatchInfo.Capture(e);
            }
        })
        { IsBackground = true, Name = ThreadName };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        error?.Throw();
    }

    /// <summary>
    /// Whether a failed <c>ShellExecuteEx</c> with this last error is the user declining a prompt the
    /// shell showed (elevation, security warning): <c>ERROR_CANCELLED</c>, when the shell's UI was
    /// allowed. Without that UI (<paramref name="showErrors"/> <c>false</c>) nobody was asked, so the
    /// same code is a failure.
    /// </summary>
    internal static bool IsDeclinedByUser(int lastError, bool showErrors) => showErrors && lastError == ErrorCancelled;

    /// <summary>
    /// The folder of a disk or UNC file (<c>C:\Dir</c> for <c>C:\Dir\File.txt</c>, <c>\\server\share</c>
    /// for a file at the root of a share); <c>null</c> for anything else (a <c>shell:</c> or
    /// <c>::{CLSID}</c> name, a relative path).
    /// </summary>
    internal static string? WorkingDirectory(string path) =>
        Path.IsPathFullyQualified(path) ? Path.GetDirectoryName(path) : null;

    private static void ExecuteHere(string target, string? verb, bool showErrors, string? directory)
    {
        var info = new ShellExecuteInfo
        {
            cbSize = Marshal.SizeOf<ShellExecuteInfo>(),
            fMask = ShellNative.SeeMaskNoAsync | (showErrors ? 0 : ShellNative.SeeMaskFlagNoUi),
            lpVerb = verb,
            lpFile = target,
            lpDirectory = directory,
            nShow = ShellNative.SwShowNormal,
        };

        if (!ShellNative.ShellExecuteEx(ref info))
        {
            var code = Marshal.GetLastPInvokeError();
            if (IsDeclinedByUser(code, showErrors))
            {
                return;
            }

            throw new Win32Exception(code, $"The shell could not open '{target}': {Marshal.GetPInvokeErrorMessage(code)}");
        }
    }
}
