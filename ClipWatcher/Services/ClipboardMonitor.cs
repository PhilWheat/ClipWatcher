using System.Runtime.InteropServices;

namespace ClipWatcher.Services;

public sealed class ClipboardMonitor : IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    public bool IsEnabled { get; set; } = true;
    public event Action? ClipboardUpdated;

    private IntPtr _hwnd;
    private bool _registered;

    public void Attach(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _registered = AddClipboardFormatListener(hwnd);
    }

    public void Detach()
    {
        if (!_registered) return;
        RemoveClipboardFormatListener(_hwnd);
        _registered = false;
    }

    public void WndProc(ref Message m)
    {
        if (m.Msg == WM_CLIPBOARDUPDATE && IsEnabled)
            ClipboardUpdated?.Invoke();
    }

    public void Dispose() => Detach();
}
