using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Controls;

namespace FolderSwitch.Windows.SystemServices;

/// <summary>
/// The frame the Desktop Window Manager draws around a borderless top-level window: rounded corners on Windows 11,
/// and the drop shadow.
/// </summary>
/// <remarks>
/// <para>
/// <b>Corners.</b> Windows 11 rounds a window without a system frame only when asked
/// (<c>DWMWA_WINDOW_CORNER_PREFERENCE</c>); Windows 10 has no rounded corners at all, so a window there draws a
/// 1 px border of its own instead (see <see cref="SupportsRoundedCorners"/>).
/// </para>
/// <para>
/// <b>Shadow.</b> A window without a system frame gets no shadow either: the non-client rendering is enabled and the
/// frame is extended 1 px into the client area, which makes the DWM draw the shadow of a frame. The window must paint
/// its whole client area opaquely, which hides the extended frame itself.
/// </para>
/// <para>Plain Win32 calls on a window of the calling process; any thread. Nothing is thrown: each call says whether it worked.</para>
/// </remarks>
public static class WindowFrame
{
    /// <summary>Whether the system rounds window corners (Windows 11).</summary>
    public static bool SupportsRoundedCorners => WindowsVersion.IsWindows11;

    /// <summary>
    /// Asks Windows 11 for rounded corners: the usual radius, or the small one (<paramref name="small"/>). Does nothing
    /// and returns <c>false</c> on Windows 10.
    /// </summary>
    public static unsafe bool SetRoundedCorners(nint hwnd, bool small)
    {
        if (hwnd == 0 || !SupportsRoundedCorners)
        {
            return false;
        }

        try
        {
            var preference = small ? DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUNDSMALL : DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
            return PInvoke.DwmSetWindowAttribute(
                (HWND)hwnd,
                DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
                &preference,
                (uint)sizeof(DWM_WINDOW_CORNER_PREFERENCE)).Succeeded;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Gives a borderless window the shadow of a system frame. Returns whether it worked.</summary>
    public static unsafe bool EnableShadow(nint hwnd)
    {
        if (hwnd == 0)
        {
            return false;
        }

        try
        {
            var policy = DWMNCRENDERINGPOLICY.DWMNCRP_ENABLED;
            var enabled = PInvoke.DwmSetWindowAttribute(
                (HWND)hwnd,
                DWMWINDOWATTRIBUTE.DWMWA_NCRENDERING_POLICY,
                &policy,
                (uint)sizeof(DWMNCRENDERINGPOLICY)).Succeeded;

            var margins = new MARGINS { cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 0, cyBottomHeight = 1 };
            return enabled && PInvoke.DwmExtendFrameIntoClientArea((HWND)hwnd, &margins).Succeeded;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
