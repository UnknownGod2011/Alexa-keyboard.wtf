namespace AlexaDesktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var form = new CompanionForm();
        if (args.Contains("--ui-smoke", StringComparer.Ordinal))
        {
            form.Shown += async (_, _) =>
            {
                try
                {
                    await form.VerifyUiAsync();
                    Console.WriteLine("PASS: graphical control events, authenticated MCP discovery/status/save/load/errors/reconnect and control rendering.");
                    Console.WriteLine("NOTE: hosted control rendering does not verify an interactive user desktop or visible Notepad launch.");
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
