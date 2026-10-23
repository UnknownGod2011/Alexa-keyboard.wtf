using System.Runtime.InteropServices;

internal sealed class ExplorerWindowProbe(string approvedDirectory) : IDisposable
{
    private IntPtr _observed;
    internal void VerifyOpened()
    {
        for (var i = 0; i < 60; i++)
        {
            if (FindOrClose(close: false))
            {
                Console.WriteLine("PASS: actual Windows Explorer displays the exact approved workspace directory in a visible folder window.");
                return;
            }
            Thread.Sleep(500);
        }
        throw new InvalidOperationException("Explorer did not expose a visible window for the exact approved workspace folder within 30 seconds.");
    }

    private bool FindOrClose(bool close)
    {
        object? shellObject = null, windowsObject = null;
        try
        {
            var type = Type.GetTypeFromProgID("Shell.Application") ?? throw new InvalidOperationException("Windows Shell automation is unavailable.");
            shellObject = Activator.CreateInstance(type)!;
            dynamic shell = shellObject;
            windowsObject = shell.Windows();
            dynamic windows = windowsObject;
            for (var i = 0; i < (int)windows.Count; i++)
            {
                object? windowObject = null;
                try
                {
                    windowObject = windows.Item(i);
                    if (windowObject is null) continue;
                    dynamic window = windowObject;
                    string location = window.LocationURL;
                    if (!Uri.TryCreate(location, UriKind.Absolute, out var uri) || !uri.IsFile ||
                        !Path.TrimEndingDirectorySeparator(Path.GetFullPath(uri.LocalPath)).Equals(
                            Path.TrimEndingDirectorySeparator(Path.GetFullPath(approvedDirectory)), StringComparison.OrdinalIgnoreCase)) continue;
                    var handle = new IntPtr((long)window.HWND);
                    if (close)
                    {
                        if (handle == _observed) { window.Quit(); return true; }
                    }
                    else if (IsWindowVisible(handle)) { _observed = handle; return true; }
                }
                catch (COMException) { /* Explorer may close or navigate while enumerating. */ }
                finally { if (windowObject is not null && Marshal.IsComObject(windowObject)) Marshal.FinalReleaseComObject(windowObject); }
            }
            return false;
        }
        finally
        {
            if (windowsObject is not null && Marshal.IsComObject(windowsObject)) Marshal.FinalReleaseComObject(windowsObject);
            if (shellObject is not null && Marshal.IsComObject(shellObject)) Marshal.FinalReleaseComObject(shellObject);
        }
    }

    public void Dispose()
    {
        if (_observed == IntPtr.Zero) return;
        try { FindOrClose(close: true); }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        { Console.Error.WriteLine("Owned Explorer window cleanup: " + ex.GetType().Name); }
    }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);
}
