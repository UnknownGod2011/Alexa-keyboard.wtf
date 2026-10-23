namespace AlexaDesktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var smoke = args.Contains("--ui-smoke", StringComparer.Ordinal);
        using var form = smoke ? new CompanionForm(new RecordingVoice()) : new CompanionForm();
        if (smoke)
        {
            form.Shown += async (_, _) =>
            {
                try
                {
                    await form.VerifyUiAsync();
                    Console.WriteLine("PASS: graphical control events, authenticated MCP discovery/status/save/load/errors/reconnect and control rendering.");
                    Console.WriteLine("NOTE: hosted controls use a recording voice adapter; they do not verify microphone/speaker behavior, an interactive desktop or visible Notepad launch.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("FAIL: graphical MCP smoke: " + ex.Message);
                    Environment.ExitCode = 1;
                }
                finally { form.Close(); }
            };
        }
        Application.Run(form);
    }
}
