using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;
using Windows.Win32.System.Threading;

namespace FolderSwitch.Windows.Dialogs;

/// <summary>
/// A block of memory allocated in the process that owns a window, for the window messages whose
/// pointers the system does not marshal across processes (<c>CDM_*</c>, <c>TVM_GETITEMW</c>,
/// <c>BFFM_SETSELECTIONW</c>...). Needs the same user and an integrity level not above ours: opening
/// an elevated process fails and <see cref="Open"/> returns <c>null</c>.
/// </summary>
/// <remarks>
/// A message that times out is not withdrawn: the window may still handle it later and write through
/// its pointer. So a block passed to a message that did not complete is never freed:
/// <see cref="Send"/> abandons it (<see cref="IsAbandoned"/>) and <see cref="Dispose"/> then leaves it
/// allocated in the other process (a few hundred bytes, until that process ends). The caller must not
/// send that window anything more in the same lookup.
/// </remarks>
internal sealed unsafe class RemoteMemory : IDisposable
{
    private readonly SafeFileHandle _process;
    private void* _address;
    private bool _abandoned;

    private RemoteMemory(SafeFileHandle process, void* address, int size, bool is32Bit)
    {
        _process = process;
        _address = address;
        Size = size;
        Is32BitProcess = is32Bit;
    }

    /// <summary>The address of the block in the other process.</summary>
    public nint Address => (nint)_address;

    public int Size { get; }

    /// <summary>
    /// Whether the other process is a 32-bit (WOW64) one: structures holding pointers then take their
    /// 32-bit layout.
    /// </summary>
    public bool Is32BitProcess { get; }

    /// <summary>Whether a message carrying the block did not complete: the block is left allocated.</summary>
    public bool IsAbandoned => _abandoned;

    /// <summary>Allocates <paramref name="size"/> zeroed bytes in the process owning <paramref name="window"/>.</summary>
    public static RemoteMemory? Open(nint window, int size)
    {
        if (PInvoke.GetWindowThreadProcessId((HWND)window, out var processId) == 0)
        {
            return null;
        }

        var process = PInvoke.OpenProcess_SafeHandle(
            PROCESS_ACCESS_RIGHTS.PROCESS_VM_OPERATION
            | PROCESS_ACCESS_RIGHTS.PROCESS_VM_READ
            | PROCESS_ACCESS_RIGHTS.PROCESS_VM_WRITE
            | PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION,
            false,
            processId);
        if (process.IsInvalid)
        {
            process.Dispose();
            return null;
        }

        // FolderSwitch is a 64-bit process: a WOW64 process is a 32-bit one.
        var is32Bit = PInvoke.IsWow64Process(process, out var wow64) && wow64;
        var address = PInvoke.VirtualAllocEx(
            (HANDLE)process.DangerousGetHandle(),
            null,
            (nuint)size,
            VIRTUAL_ALLOCATION_TYPE.MEM_COMMIT | VIRTUAL_ALLOCATION_TYPE.MEM_RESERVE,
            PAGE_PROTECTION_FLAGS.PAGE_READWRITE);

        // A 32-bit process cannot use an address above 4 GB.
        if (address is null || (is32Bit && (ulong)address > uint.MaxValue))
        {
            if (address is not null)
            {
                PInvoke.VirtualFreeEx(process, address, 0, VIRTUAL_FREE_TYPE.MEM_RELEASE);
            }

            process.Dispose();
            return null;
        }

        return new RemoteMemory(process, address, size, is32Bit);
    }

    /// <summary>Copies <paramref name="data"/> to the block, at <paramref name="offset"/>.</summary>
    public bool Write(int offset, ReadOnlySpan<byte> data) =>
        InRange(offset, data.Length) && PInvoke.WriteProcessMemory(_process, (byte*)_address + offset, data);

    /// <summary>Fills <paramref name="buffer"/> from the block, at <paramref name="offset"/>.</summary>
    public bool Read(int offset, Span<byte> buffer) =>
        InRange(offset, buffer.Length) && PInvoke.ReadProcessMemory(_process, (byte*)_address + offset, buffer);

    /// <summary>
    /// Sends <paramref name="message"/> to <paramref name="window"/> with <c>lParam</c> pointing at
    /// <paramref name="offset"/> in the block, waiting at most <paramref name="timeoutMs"/>. The result,
    /// or <c>null</c> if the message failed or timed out, in which case the block is abandoned.
    /// </summary>
    public nuint? Send(nint window, uint message, nint wParam, int offset, uint timeoutMs)
    {
        if (!InRange(offset, 1))
        {
            return null;
        }

        var result = WindowMessages.Send(window, message, wParam, (nint)((byte*)_address + offset), timeoutMs);
        if (result is null)
        {
            _abandoned = true;
        }

        return result;
    }

    /// <summary>Frees the block, unless it was abandoned, and closes the process handle.</summary>
    public void Dispose()
    {
        if (_address is not null && !_abandoned)
        {
            PInvoke.VirtualFreeEx(_process, _address, 0, VIRTUAL_FREE_TYPE.MEM_RELEASE);
            _address = null;
        }

        _process.Dispose();
    }

    private bool InRange(int offset, int length) =>
        _address is not null && !_abandoned && offset >= 0 && length >= 0 && offset + length <= Size;
}
