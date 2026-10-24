using System.ComponentModel;
using System.Runtime.InteropServices;

namespace EffortLess.App;

// mpv creates its own renderer HWND below this host. XAML stays outside its airspace.
internal sealed class ChildVideoWindow : IDisposable
{
    private readonly string _className = "EffortLess.Video." + Guid.NewGuid().ToString("N");
    private readonly Win32.WindowProc _procedure;
    private readonly nint _instance = Win32.GetModuleHandle(null);
    private bool _registered;
    private bool _visible = true;
    public nint Handle { get; private set; }
    public event Action<int>? KeyDown;
    public event Action? PointerMoved;
    private bool _cursorHidden;
    public event Action? Clicked;
    public event Action? DoubleClicked;
    public event Action<Exception>? Error;
    public event Action<IReadOnlyList<string>>? FilesDropped;

    public ChildVideoWindow(nint parent)
    {
        _procedure = Procedure;
        var windowClass = new Win32.WindowClass
        {
            Size = (uint)Marshal.SizeOf<Win32.WindowClass>(), Style = 8,
            Procedure = _procedure, Instance = _instance, ClassName = _className,
            Cursor = Win32.LoadCursor(0, 32512), Background = Win32.GetStockObject(4)
        };
        if (Win32.RegisterClassEx(ref windowClass) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot register the video host.");
        _registered = true;
        Handle = Win32.CreateWindowEx(0, _className, "", 0x56000000, 0, 0, 1, 1, parent, 0, _instance, 0);
        if (Handle == 0)
        {
            int error = Marshal.GetLastWin32Error();
            Dispose();
            throw new Win32Exception(error, "Cannot create the video host.");
        }
        // The video HWND has its own airspace, so XAML's Drop event cannot see it.
        // Shell file drops are handled here without subclassing mpv's renderer.
        Win32.DragAcceptFiles(Handle, true);
    }

    public void MoveAndResize(int x, int y, int width, int height)
    {
        if (Handle != 0)
            // HWND_TOP places this bounded native host above the XAML island.
            // SWP_NOZORDER would leave it covered by the island's black placeholder.
            // SWP_NOACTIVATE keeps layout from stealing keyboard focus.
            Win32.SetWindowPos(Handle, 0, x, y, Math.Max(1, width), Math.Max(1, height), 0x0010);
    }

    public void RouteRendererInputToHost()
    {
        // Disabled renderer children keep drawing; mouse input falls through to this host.
        // No cross-thread subclass or managed callback is installed on mpv's render thread.
        if (Handle != 0)
            Win32.EnumChildWindows(Handle, (child, _) => { Win32.EnableWindow(child, false); return true; }, 0);
    }
    public void SetVisible(bool visible)
    {
        if (Handle == 0 || _visible == visible) return;
        _visible = visible;
        Win32.ShowWindow(Handle, visible ? 8 : 0); // SW_SHOWNA / SW_HIDE.
    }

    public void SetCursorHidden(bool hidden)
    {
        if (_cursorHidden == hidden) return;
        _cursorHidden = hidden;
        // SetCursor changes this window's cursor, avoiding global ShowCursor counts.
        if (Win32.GetCursorPos(out var point) &&
            (Win32.WindowFromPoint(point) == Handle || Win32.IsChild(Handle, Win32.WindowFromPoint(point))))
            Win32.SetCursor(hidden ? 0 : Win32.LoadCursor(0, 32512));
    }

    private nint Procedure(nint hwnd, uint message, nint wParam, nint lParam)
    {
        try
        {
            switch (message)
            {
                case 0x0233:
                    try
                    {
                        uint count = Win32.DragQueryFile(wParam, uint.MaxValue, null, 0);
                        if (count > 1000) throw new ArgumentException("Drop at most 1,000 files at once.");
                        var files = new List<string>();
                        for (uint i = 0; i < count; i++)
                        {
                            uint length = Win32.DragQueryFile(wParam, i, null, 0);
                            if (length > 32767) throw new ArgumentException("The dropped path is too long.");
                            var path = new System.Text.StringBuilder((int)length + 1);
                            Win32.DragQueryFile(wParam, i, path, length + 1);
                            files.Add(path.ToString());
                        }
                        FilesDropped?.Invoke(files);
                    }
                    finally { Win32.DragFinish(wParam); }
                    return 0;
                case 0x0200: PointerMoved?.Invoke(); break; // WM_MOUSEMOVE
                case 0x0020: // WM_SETCURSOR, client area only.
                    if ((lParam.ToInt64() & 0xffff) == 1)
                    {
                        Win32.SetCursor(_cursorHidden ? 0 : Win32.LoadCursor(0, 32512));
                        return 1;
                    }
                    break;
                case 0x0100: KeyDown?.Invoke((int)wParam); return 0;
                case 0x0201: Win32.SetFocus(hwnd); Clicked?.Invoke(); return 0;
                case 0x0203: DoubleClicked?.Invoke(); return 0;
            }
        }
        catch (Exception ex)
        {
            // Exceptions must never escape an unmanaged window procedure.
            System.Diagnostics.Debug.WriteLine(ex);
            try { Error?.Invoke(ex); } catch { }
        }
        return Win32.DefWindowProc(hwnd, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (Handle != 0) { Win32.DestroyWindow(Handle); Handle = 0; }
        if (_registered) { Win32.UnregisterClass(_className, _instance); _registered = false; }
        GC.KeepAlive(_procedure);
    }
}
