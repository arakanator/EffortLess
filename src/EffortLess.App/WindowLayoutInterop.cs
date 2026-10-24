using System.Runtime.InteropServices;

namespace EffortLess.App;

internal static class WindowLayoutInterop
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X, Y; }

    [DllImport("user32.dll", EntryPoint = "FindWindowExW", CharSet = CharSet.Unicode)]
    private static extern nint FindWindowEx(nint parent, nint after, string className, string? title);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint hwnd, ref Point point);
    [DllImport("user32.dll", EntryPoint = "IsIconic")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsMinimized(nint hwnd);

    internal static bool AlignContentToClient(nint owner)
    {
        if (IsMinimized(owner) || !GetClientRect(owner, out var client) ||
            client.Right <= 0 || client.Bottom <= 0) return false;

        // WinUI 3 / pinned Windows App SDK 1.8 hosts this Window's XAML in this
        // direct child. After fullscreen + minimize/restore it can apply the
        // nonclient inset twice. Correct only a measured rectangle mismatch;
        // do not recreate the island, change its Z order, or take focus.
        nint island = FindWindowEx(owner, 0, "Microsoft.UI.Content.DesktopChildSiteBridge", null);
        if (island == 0 || !GetWindowRect(island, out var actual)) return true;
        var origin = new Point();
        if (!ClientToScreen(owner, ref origin)) return true;
        if (actual.Left == origin.X && actual.Top == origin.Y &&
            actual.Right - actual.Left == client.Right && actual.Bottom - actual.Top == client.Bottom)
            return true;

        return Win32.SetWindowPos(island, 0, 0, 0, client.Right, client.Bottom, 0x0014);
    }
}
