// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Globalization;
using System.IO;
using System.Text;
using FolderSwitch.Core.Persistence;

namespace FolderSwitch.App.Diagnostics;

/// <summary>
/// The local debug log, <c>%APPDATA%\FolderSwitch\logs\folderswitch.log</c> (<see cref="DefaultDirectory"/>, next to
/// <c>config.json</c>: it survives an update and an uninstall like the configuration, and stays out of the folder that
/// Velopack owns): startup steps, timings, and the errors FolderSwitch recovers from. Never sent anywhere (no
/// telemetry). Small and rolling: past <see cref="MaxBytes"/> the file becomes <c>folderswitch.1.log</c> (replacing the
/// previous one) and a new file starts.
/// </summary>
/// <remarks>
/// <para>
/// Any thread. Never throws: a log that cannot be written is silently dropped. A self-test and a snapshot run open it
/// elsewhere (their throwaway folder, their output folder) and never create anything in the user's data folder.
/// </para>
/// <para>
/// A roll that fails (<c>folderswitch.1.log</c> held open by a tool that does not share it for deletion, say) does not
/// stop the log: the current file is opened again and written on, and the roll is tried again once it has grown by
/// <see cref="RollRetryBytes"/> more.
/// </para>
/// </remarks>
internal static class DebugLog
{
    /// <summary>Size at which the log rolls over.</summary>
    public const long MaxBytes = 512 * 1024;

    /// <summary>After a roll failed, how much more the current file is written before the roll is tried again.</summary>
    public const long RollRetryBytes = 32 * 1024;

    private const string FileName = "folderswitch.log";

    private static readonly Lock Gate = new();
    private static string? _path;
    private static StreamWriter? _writer;

    /// <summary>The size past which the next line rolls the file: <see cref="MaxBytes"/>, or more after a failed roll.</summary>
    private static long _rollAt = MaxBytes;

    /// <summary>The folder of the log of a normal run: <c>logs</c>, in the data folder of the configuration.</summary>
    public static string DefaultDirectory { get; } = Path.Combine(ConfigStore.DefaultDirectory, "logs");

    /// <summary>The current log file: where it was opened, or where a normal run opens it.</summary>
    public static string FilePath => _path ?? Path.Combine(DefaultDirectory, FileName);

    /// <summary>
    /// Opens the log in <paramref name="directory"/> (created if needed); until then, and if it cannot be opened,
    /// entries are dropped.
    /// </summary>
    public static void Open(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        lock (Gate)
        {
            if (_writer is not null)
            {
                return;
            }

            try
            {
                System.IO.Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, FileName);
                _writer = OpenWriter(path);
                _path = path;
                _rollAt = MaxBytes;
            }
            catch (Exception e) when (IsIoError(e))
            {
                _writer = null;
            }
        }
    }

    public static void Info(string message) => Write("INFO ", message, null);

    public static void Warn(string message, Exception? exception = null) => Write("WARN ", message, exception);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    /// <summary>Flushes and closes the log; later entries are dropped.</summary>
    public static void Close()
    {
        lock (Gate)
        {
            try
            {
                _writer?.Dispose();
            }
            catch (Exception e) when (IsIoError(e))
            {
                // Nothing left to report it to.
            }

            _writer = null;
        }
    }

    private static void Write(string level, string message, Exception? exception)
    {
        var line = new StringBuilder()
            .Append(Timestamp())
            .Append(' ').Append(level)
            .Append(" [").Append(Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture)).Append("] ")
            .Append(message);
        if (exception is not null)
        {
            line.AppendLine().Append(exception);
        }

        lock (Gate)
        {
            if (_writer is null || _path is null)
            {
                return;
            }

            try
            {
                if (_writer.BaseStream.Length > _rollAt)
                {
                    Roll(_path);
                }

                _writer?.WriteLine(line.ToString());
            }
            catch (Exception e) when (IsIoError(e))
            {
                // A full disk or a removed folder must not stop FolderSwitch.
            }
        }
    }

    /// <summary>
    /// Under the gate: the current file becomes the previous one, and a new file starts. When the file cannot be moved,
    /// the current one is opened again (logging goes on in it) and the roll waits for <see cref="RollRetryBytes"/> more.
    /// Only a file that cannot be opened at all leaves the log closed.
    /// </summary>
    private static void Roll(string path)
    {
        _writer!.Dispose();
        _writer = null;
        try
        {
            File.Move(path, Path.ChangeExtension(path, ".1.log"), overwrite: true);
            _rollAt = MaxBytes;
        }
        catch (Exception e) when (IsIoError(e))
        {
            _writer = OpenWriter(path);
            _rollAt = _writer.BaseStream.Length + RollRetryBytes;
            _writer.WriteLine(FormattableString.Invariant(
                $"{Timestamp()} WARN  [{Environment.CurrentManagedThreadId}] The log could not roll over ({e.Message}): it goes on in this file"));
            return;
        }

        _writer = OpenWriter(path);
    }

    private static string Timestamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private static StreamWriter OpenWriter(string path)
    {
        // Shared for reading (a text editor, a tail) and deletion; flushed at every line, since a crash must not lose
        // the lines that explain it.
        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        return new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
    }

    private static bool IsIoError(Exception e) =>
        e is IOException or UnauthorizedAccessException or System.Security.SecurityException or ObjectDisposedException;
}
