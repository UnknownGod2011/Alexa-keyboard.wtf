using AlexaBridge;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Speech.Recognition;
using System.Speech.Synthesis;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("ALEXA_RUN_NATIVE_DESKTOP_CHECKS") != "1")
        {
            Console.Error.WriteLine("Refusing native desktop checks without explicit Windows test opt-in ALEXA_RUN_NATIVE_DESKTOP_CHECKS=1.");
            return 2;
        }
        var existing = Process.GetProcessesByName("notepad");
        var occupied = existing.Length > 0;
        foreach (var process in existing) process.Dispose();
        if (occupied) { Console.Error.WriteLine("Refusing to interfere with an existing Notepad session."); return 2; }
        var started = DateTime.Now;
        var owned = new Dictionary<int, DateTime>();
        try
        {
            var fixture = new NativeMcpFixture();
            try
            {
                fixture.StartAsync().GetAwaiter().GetResult();
                fixture.RestoreThroughMcpAsync().GetAwaiter().GetResult();
            }
            finally { fixture.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            IntPtr window = IntPtr.Zero;
            Rectangle bounds = default;
            for (var i = 0; i < 60 && window == IntPtr.Zero; i++)
            {
                foreach (var process in Process.GetProcessesByName("notepad"))
                {
                    using (process)
                    {
                        try { if (process.StartTime >= started.AddSeconds(-1)) owned.TryAdd(process.Id, process.StartTime); }
                        catch (InvalidOperationException) { }
                    }
                }
                EnumWindows((handle, _) => {
                    GetWindowThreadProcessId(handle, out var pid);
                    if (!owned.ContainsKey((int)pid) || !IsWindowVisible(handle) || !GetWindowRect(handle, out var rect)) return true;
                    var candidate = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
                    if (candidate.Width < 100 || candidate.Height < 100 || !Screen.AllScreens.Any(s => s.Bounds.IntersectsWith(candidate))) return true;
                    window = handle; bounds = candidate; return false;
                }, IntPtr.Zero);
                if (window == IntPtr.Zero) Thread.Sleep(500);
            }
            if (window == IntPtr.Zero) throw new InvalidOperationException("Actual Notepad launch did not produce an observable visible window within 30 seconds.");
            Console.WriteLine($"PASS: real Windows Notepad has an owned visible top-level window on the runner desktop ({bounds.Width}x{bounds.Height}).");
            var directory = Path.GetFullPath(Path.Combine("artifacts", "native"));
            Directory.CreateDirectory(directory);
            using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var dc = graphics.GetHdc();
                bool captured;
                try { captured = PrintWindow(window, dc, 2); }
                finally { graphics.ReleaseHdc(dc); }
                if (captured) bitmap.Save(Path.Combine(directory, "notepad-window.png"), System.Drawing.Imaging.ImageFormat.Png);
                Console.WriteLine(captured ? "Captured the actual owned Notepad window with PrintWindow; inspect the artifact." : "NOTE: window capture unavailable; visible-window state was observed through Win32 only.");
            }
            // No microphone or speaker playback is used here. Test native synthesis to an in-memory WAV instead.
            using var speech = new SpeechSynthesizer();
            if (speech.GetInstalledVoices().Any(v => v.Enabled))
            {
                using var wav = new MemoryStream();
                speech.SetOutputToWaveStream(wav);
                speech.Speak("Local companion speech output check.");
                speech.SetOutputToNull();
                var bytes = wav.ToArray();
                if (bytes.Length <= 44 || System.Text.Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF")
                    throw new InvalidOperationException("Native speech did not generate valid WAV audio.");
                Console.WriteLine($"PASS: installed Windows speech voice generated {bytes.Length} bytes of WAV audio without speaker playback.");
            }
            else Console.WriteLine("UNVERIFIED: no enabled Windows synthesis voice is installed on this runner.");
            Console.WriteLine($"Installed recognition engines: {SpeechRecognitionEngine.InstalledRecognizers().Count}. No microphone was opened.");
            Console.WriteLine("NOTE: this uses an isolated MCP host with production tools and simulated local-console input. It does not establish human consent UX or end-user Windows 10/11 verification.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: native Windows checks: " + ex.GetType().Name + ": " + ex.Message);
            return 1;
        }
        finally
        {
            foreach (var entry in owned)
            {
                try
                {
                    using var process = Process.GetProcessById(entry.Key);
                    // A PID can be reused; only close the exact process this fixture observed.
                    if (process.ProcessName != "notepad" || process.StartTime != entry.Value) continue;
                    process.CloseMainWindow();
                    if (!process.WaitForExit(3000)) { process.Kill(); process.WaitForExit(3000); }
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
                { Console.Error.WriteLine("Native test cleanup: " + ex.GetType().Name); }
            }
        }
    }

    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr window, IntPtr deviceContext, uint flags);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
}
