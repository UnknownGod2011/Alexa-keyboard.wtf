using AlexaClient;
using ModelContextProtocol;
using System.Text.Json;

namespace AlexaDesktop;

public sealed class CompanionForm : Form
{
    private static readonly Color Background = Color.FromArgb(13, 19, 31);
    private static readonly Color Surface = Color.FromArgb(23, 32, 48);
    private static readonly Color Muted = Color.FromArgb(153, 170, 193);
    private static readonly Color Accent = Color.FromArgb(85, 220, 206);
    private readonly FlowLayoutPanel _history = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20) };
    private readonly TextBox _input = new() { Dock = DockStyle.Fill, PlaceholderText = "Try status, save workspace, restore workspace, or tools" };
    private readonly TextBox _name = new() { Dock = DockStyle.Top, Text = "Development", MaxLength = 64 };
    private readonly TextBox _summary = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, MaxLength = 1500, PlaceholderText = "A non-sensitive task summary to save…" };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Muted, Text = "Disconnected" };
    private readonly ListBox _tools = new() { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None };
    private readonly Button _connect = new() { Text = "Connect", AutoSize = true };
    private readonly Button _send = new() { Text = "Send", AutoSize = true };
    private readonly Button _cancel = new() { Text = "Cancel request", AutoSize = true, Enabled = false };
    private readonly List<Button> _shortcuts = [];
    private readonly CancellationTokenSource _lifetime = new();
    private readonly OptionalChat _chat = OptionalChat.FromEnvironment();
    private readonly Font _cardTitleFont = new("Segoe UI Semibold", 10);
    private CancellationTokenSource? _operation;
    private Task _currentTask = Task.CompletedTask;
    private BridgeConnection? _bridge;
    private bool _closing;
    private bool _allowClose;
    private bool _resourcesDisposed;

    public CompanionForm()
    {
        Text = "keyboard.wtf × Alexa | Local companion";
        Size = new Size(1120, 790);
        MinimumSize = new Size(900, 650);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Background;
        ForeColor = Color.WhiteSmoke;
        Font = new Font("Segoe UI", 10);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 18, 24, 12), ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var identity = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        identity.Controls.Add(new Label { Text = "keyboard.wtf × Alexa", AutoSize = true, Font = new Font("Segoe UI Semibold", 21), ForeColor = Accent });
        identity.Controls.Add(new Label { Text = "Local MCP simulator · Manual commands need no AI key", AutoSize = true, ForeColor = Muted });
        header.Controls.Add(identity, 0, 0);
        var connection = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        connection.Controls.Add(_status);
        connection.Controls.Add(_connect);
        header.Controls.Add(connection, 1, 0);
        root.Controls.Add(header, 0, 0);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 0, 20, 0) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        body.Controls.Add(_history, 0, 0);
        var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(16), ColumnCount = 1, RowCount = 6 };
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        sidebar.Controls.Add(new Label { Text = "TASK / WORKSPACE", AutoSize = true, ForeColor = Accent }, 0, 0);
        sidebar.Controls.Add(_name, 0, 1);
        sidebar.Controls.Add(new Label { Text = "Summary (stored as plaintext)", AutoSize = true, ForeColor = Muted }, 0, 2);
        sidebar.Controls.Add(_summary, 0, 3);
        sidebar.Controls.Add(new Label { Text = "DISCOVERED MCP TOOLS", AutoSize = true, ForeColor = Accent, Padding = new Padding(0, 14, 0, 0) }, 0, 4);
        sidebar.Controls.Add(_tools, 0, 5);
        body.Controls.Add(sidebar, 1, 0);
        root.Controls.Add(body, 0, 1);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 12, 24, 14), ColumnCount = 1, RowCount = 3 };
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var composer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        composer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        composer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        composer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        composer.Controls.Add(_input, 0, 0);
        composer.Controls.Add(_send, 1, 0);
        composer.Controls.Add(_cancel, 2, 0);
        footer.Controls.Add(composer, 0, 0);
        var shortcuts = new FlowLayoutPanel { Dock = DockStyle.Fill };
        foreach (var pair in new[] { ("Desktop status", "status"), ("Save handoff", "save"), ("Load handoff", "load"), ("Open Notepad", "notepad"), ("Save workspace", "save workspace"), ("Restore", "restore workspace") })
        {
            var button = new Button { Text = pair.Item1, AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
            button.Click += (_, _) => StartCommand(pair.Item2);
            _shortcuts.Add(button);
            shortcuts.Controls.Add(button);
        }
        footer.Controls.Add(shortcuts, 0, 1);
        footer.Controls.Add(new Label { Text = "Local simulator. Production Alexa+ connectivity is not configured.", AutoSize = true, ForeColor = Muted, Font = new Font("Segoe UI", 8) }, 0, 2);
        root.Controls.Add(footer, 0, 2);

        StyleControls(this);
        _send.Click += (_, _) => StartCommand(_input.Text);
        _connect.Click += (_, _) => _currentTask = RunOperationAsync(ConnectAsync);
        _cancel.Click += (_, _) => _operation?.Cancel();
        _input.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; StartCommand(_input.Text); } };
        Shown += (_, _) =>
        {
            AddCard("Welcome", "Connect to your local bridge, then choose an action. Save and load use the task fields on the right. Notepad requests require operator opt-in and a fresh approval in the bridge console.", Accent);
            AddCard("Optional AI", _chat.Status, Muted);
            _currentTask = RunOperationAsync(ConnectAsync);
        };
        FormClosing += OnClosing;
        _history.SizeChanged += (_, _) => ResizeCards();
        SetBusy(false);
    }

    private void StartCommand(string text)
    {
        if (_operation is not null || _closing || string.IsNullOrWhiteSpace(text)) return;
        var command = ManualCommands.Parse(text);
        var name = _name.Text;
        var summary = _summary.Text;
        Dictionary<string, object?>? workspaceArguments = null;
        if (command == ManualCommand.SaveWorkspace)
        {
            using var dialog = new WorkspaceDialog(name);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            workspaceArguments = new() { ["name"] = name, ["summary"] = summary,
                ["relativeFolder"] = dialog.RelativeFolder, ["openFolder"] = dialog.OpenFolder, ["openNotepad"] = dialog.OpenNotepad };
        }
        _input.Clear();
        AddCard("You", text, Color.WhiteSmoke);
        _currentTask = RunOperationAsync(async ct =>
        {
            if (command == ManualCommand.Help)
            {
                AddCard("Manual mode", "Supported commands: status, tools, save, load, open notepad, workspace policy, save workspace, load workspace, restore workspace. Workspace actions use the name and summary fields. Use ask <question> for configured optional AI; ai status or ai reset for its settings/history.", Muted);
                return;
            }
            if (command == ManualCommand.AiStatus) { AddCard("Optional AI", _chat.Status, Muted); return; }
            if (command == ManualCommand.AiReset) { await _chat.ResetAsync(ct); AddCard("Optional AI", "Ask/reply history cleared from this client session.", Muted); return; }
            if (command == ManualCommand.Ask)
            {
                _status.Text = "Asking configured AI…";
                var reply = await _chat.AskAsync(ManualCommands.AskPrompt(text), ct);
                AddCard("AI answer · " + (reply.Refused ? "refusal" : reply.Incomplete ? "partial" : "text only"),
                    reply.Text + "\n\nNo desktop action was executed by this answer.", reply.Incomplete || reply.Refused ? Muted : Accent);
                return;
            }
            if (_bridge is null) throw new IOException("Connect to the bridge first.");
            if (command == ManualCommand.Discover) { await DiscoverAsync(ct); return; }
            var tool = command switch
            {
                ManualCommand.Status => "get_desktop_status",
                ManualCommand.Save => "save_task_handoff",
                ManualCommand.Load => "load_task_handoff",
                ManualCommand.WorkspacePolicy => "get_workspace_policy",
                ManualCommand.SaveWorkspace => "save_workspace",
                ManualCommand.LoadWorkspace => "load_workspace",
                ManualCommand.RestoreWorkspace => "restore_workspace",
                ManualCommand.Notepad => "open_notepad",
                _ => throw new InvalidOperationException("Unsupported manual command.")
            };
            Dictionary<string, object?>? arguments = command switch
            {
                ManualCommand.Save => new() { ["name"] = name, ["summary"] = summary },
                ManualCommand.Load or ManualCommand.LoadWorkspace or ManualCommand.RestoreWorkspace => new() { ["name"] = name },
                ManualCommand.SaveWorkspace => workspaceArguments,
                _ => null
            };
            if (command is ManualCommand.Notepad or ManualCommand.RestoreWorkspace)
                AddCard("Approval", "If desktop launches are enabled, approve or deny this individual request in the server console. Cancel request stops waiting; a completed Windows launch cannot be undone.", Muted);
            _status.Text = "Executing · " + tool;
            var result = await _bridge.CallAsync(tool, arguments, ct);
            var resultText = BridgeConnection.Text(result);
            if (result.IsError != true && command is ManualCommand.Load or ManualCommand.LoadWorkspace)
            {
                using var handoff = JsonDocument.Parse(resultText);
                _summary.Text = handoff.RootElement.GetProperty("Summary").GetString() ?? "";
            }
            AddCard(tool + (result.IsError == true ? " · denied or failed" : " · completed"),
                PresentResult(tool, resultText), result.IsError == true ? Color.Salmon : Accent);
        });
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        _status.Text = "Connecting…";
        await DisconnectAsync();
        _bridge = await BridgeConnection.ConnectAsync(ct);
        await DiscoverAsync(ct);
        AddCard("Connected", "Authenticated MCP initialization and tool discovery completed over Streamable HTTP.", Accent);
    }

    private async Task DiscoverAsync(CancellationToken ct)
    {
        if (_bridge is null) throw new IOException("No MCP connection.");
        var tools = await _bridge.DiscoverAsync(ct);
        _tools.Items.Clear();
        foreach (var tool in tools) _tools.Items.Add(tool.Name);
        AddCard("Available tools", string.Join(Environment.NewLine, tools.Select(t => t.Name + " — " + t.Description)), Accent);
    }

    private async Task RunOperationAsync(Func<CancellationToken, Task> operation)
    {
        if (_operation is not null || _closing) return;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(40));
        _operation = timeout;
        SetBusy(true);
        try { await operation(timeout.Token); }
        catch (ChatUnavailableException ex)
        { if (!_closing) AddCard("Optional AI unavailable", ex.Message, Color.Salmon); }
        catch (OperationCanceledException)
        {
            if (!_closing) AddCard("Request cancelled or timed out", "No success was reported. Check any desktop action before retrying it.", Muted);
        }
        catch (Exception ex) when (ex is McpException or IOException or UnauthorizedAccessException or HttpRequestException or InvalidOperationException or JsonException or KeyNotFoundException or FormatException)
        {
            if (!_closing) AddCard("Connection or request failed", ex.Message + Environment.NewLine + "Start the bridge as the same OS user, then reconnect. Its local credential rotates on restart.", Color.Salmon);
            await DisconnectAsync();
        }
        finally
        {
            _operation = null;
            if (!_closing) SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _connect.Enabled = !busy;
        _send.Enabled = !busy;
        _cancel.Enabled = busy;
        _name.Enabled = !busy;
        _summary.Enabled = !busy;
        foreach (var button in _shortcuts) button.Enabled = !busy;
        _connect.Text = _bridge is null ? "Connect" : "Reconnect";
        if (!busy) _status.Text = _bridge is null ? "Disconnected" : "Connected · authenticated MCP";
    }

    private void AddCard(string title, string text, Color color)
    {
        if (_closing || IsDisposed) return;
        var card = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Surface, Padding = new Padding(16), Margin = new Padding(0, 0, 0, 14) };
        var width = Math.Max(300, _history.ClientSize.Width - 85);
        card.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(width, 0), Text = title, ForeColor = color, Font = _cardTitleFont });
        card.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(width, 0), Text = text, ForeColor = Color.WhiteSmoke, Margin = new Padding(0, 8, 0, 0), UseMnemonic = false });
        _history.Controls.Add(card);
        _history.ScrollControlIntoView(card);
    }

    private static void StyleControls(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is TextBox or ListBox) { control.BackColor = Background; control.ForeColor = Color.WhiteSmoke; }
            if (control is Button button) { button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Color.FromArgb(60, 85, 105); button.BackColor = Surface; button.ForeColor = Color.WhiteSmoke; }
            StyleControls(control);
        }
    }

    private static string PresentResult(string tool, string text)
    {
        if (tool is not ("get_desktop_status" or "load_task_handoff" or "get_workspace_policy" or "save_workspace" or "load_workspace" or "restore_workspace") || !text.TrimStart().StartsWith('{')) return text;
        using var document = JsonDocument.Parse(text);
        var data = document.RootElement;
        if (tool == "get_workspace_policy")
            return $"Approved project root: {data.GetProperty("root").GetString()}\n\nDesktop launches: {(data.GetProperty("desktopActionsEnabled").GetBoolean() ? "enabled, with fresh approval for each restore" : "disabled by the server operator")}";
        if (tool is "save_workspace" or "load_workspace")
            return $"{data.GetProperty("Name").GetString()}\nFolder: {data.GetProperty("RelativeFolder").GetString()}\nOpen folder: {data.GetProperty("OpenFolder").GetBoolean()} · Open Notepad: {data.GetProperty("OpenNotepad").GetBoolean()}\n\n{data.GetProperty("Summary").GetString()}\n\nSaved: {data.GetProperty("SavedAt").GetDateTimeOffset().ToLocalTime():g}";
        if (tool == "restore_workspace")
            return data.GetProperty("workspace").GetString() + "\n\n" +
                string.Join("\n", data.GetProperty("steps").EnumerateArray().Select(step =>
                    step.GetProperty("Action").GetString() + ": " + step.GetProperty("State").GetString() +
                    (step.GetProperty("ProcessId").ValueKind == JsonValueKind.Number ? " (PID " + step.GetProperty("ProcessId").GetInt32() + ")" : "") +
                    (step.GetProperty("Detail").ValueKind == JsonValueKind.String ? " · " + step.GetProperty("Detail").GetString() : ""))) +
                "\n\n" + data.GetProperty("note").GetString();
        if (tool == "load_task_handoff")
            return $"{data.GetProperty("Name").GetString()}\n\n{data.GetProperty("Summary").GetString()}\n\nSaved: {data.GetProperty("SavedAt").GetDateTimeOffset().ToLocalTime():g}";
        var enabled = data.GetProperty("desktopActionsEnabled").GetBoolean();
        return $"Platform: {data.GetProperty("platform").GetString()}\nDesktop launches: {(enabled ? "enabled, with approval for each request" : "disabled by the server operator")}\n\nYour task handoffs stay on this computer. No AI key is required.";
    }

    private void ResizeCards()
    {
        var width = Math.Max(300, _history.ClientSize.Width - 85);
        foreach (var card in _history.Controls.OfType<FlowLayoutPanel>())
            foreach (var label in card.Controls.OfType<Label>()) label.MaximumSize = new Size(width, 0);
    }

    // Exercises the actual button handlers and transport on a hosted Windows runner.
    // This is not an interactive-desktop or real Notepad-visibility assertion.
    internal async Task VerifyUiAsync()
    {
        await _currentTask;
        Ensure(_bridge is not null && _tools.Items.Count == 8, "initial connection/discovery failed");
        _input.Text = "status";
        _send.PerformClick();
        await _currentTask;
        Ensure(LastCard().Title == "get_desktop_status · completed" && LastCard().Body.Contains("Platform:", StringComparison.Ordinal), "status button did not return an actual tool result");
        _name.Text = "Development";
        _summary.Text = "Graphical simulator to MCP server round trip";
        _shortcuts[1].PerformClick();
        await _currentTask;
        Ensure(LastCard().Title == "save_task_handoff · completed", "save button failed");
        _summary.Clear();
        _shortcuts[2].PerformClick();
        await _currentTask;
        Ensure(LastCard().Title == "load_task_handoff · completed" && _summary.Text == "Graphical simulator to MCP server round trip" && LastCard().Body.Contains(_summary.Text, StringComparison.Ordinal), "load button did not retrieve saved data and restore the editable summary");

        _summary.Text = "Workspace MCP round trip";
        BeginInvoke((Action)(() => {
            var dialog = Application.OpenForms.OfType<WorkspaceDialog>().Single();
            dialog.FillForCheck("Example", true, true);
            dialog.AcceptButton!.PerformClick();
        }));
        _shortcuts[4].PerformClick();
        await _currentTask;
        Ensure(LastCard().Title == "save_workspace · completed", "workspace dialog/save button failed");
        _summary.Clear();
        _input.Text = "load workspace";
        _send.PerformClick();
        await _currentTask;
        Ensure(LastCard().Title == "load_workspace · completed" && _summary.Text == "Workspace MCP round trip", "workspace load did not return saved summary");
        _shortcuts[5].PerformClick();
        await _currentTask;
        Ensure(LastCard().Title == "restore_workspace · denied or failed" && LastCard().Body.Contains("disabled", StringComparison.Ordinal), "restore button bypassed operator policy");
        _input.Text = "workspace policy";
        _send.PerformClick();
        await _currentTask;
        Ensure(LastCard().Title == "get_workspace_policy · completed", "workspace policy query failed");

        var directory = Environment.GetEnvironmentVariable("ALEXA_UI_CAPTURE_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            Refresh();
            using var bitmap = new Bitmap(Width, Height);
            DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
            bitmap.Save(Path.Combine(directory, "companion-control-render.png"), System.Drawing.Imaging.ImageFormat.Png);
            var previousSize = Size;
            Size = MinimumSize;
            PerformLayout();
            Refresh();
            foreach (var control in new Control[] { _input, _send, _cancel, _connect, _name, _summary, _tools }.Concat(_shortcuts))
            {
                var bounds = RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
                Ensure(ClientRectangle.Contains(bounds), "control clipped at minimum window size: " + control.GetType().Name);
            }
            using var compact = new Bitmap(Width, Height);
            DrawToBitmap(compact, new Rectangle(0, 0, Width, Height));
            compact.Save(Path.Combine(directory, "companion-control-render-compact.png"), System.Drawing.Imaging.ImageFormat.Png);
            Size = previousSize;
        }

        _name.Text = "../escape";
        _shortcuts[1].PerformClick();
        await _currentTask;
        Ensure(LastCard().Title == "save_task_handoff · denied or failed" && _name.Enabled && _send.Enabled, "validation error did not preserve usable controls");
        _input.Text = "open notepad && delete everything";
        _send.PerformClick();
        await _currentTask;
        Ensure(LastCard().Title == "Manual mode", "unsupported input selected a desktop action");
        _input.Text = "ai status";
        _send.PerformClick();
        await _currentTask;
        Ensure(LastCard().Title == "Optional AI" && LastCard().Body.Contains("disabled", StringComparison.Ordinal), "disabled AI status is not truthful");
        _input.Text = "ask smoke question";
        _send.PerformClick();
        await _currentTask;
        Ensure(LastCard().Title == "Optional AI unavailable" && _bridge is not null && _send.Enabled, "disabled AI did not preserve no-key MCP connection");
        _connect.PerformClick();
        await _currentTask;
        Ensure(_bridge is not null && _tools.Items.Count == 8 && _connect.Enabled, "reconnect failed");
    }

    private (string Title, string Body) LastCard()
    {
        var labels = _history.Controls.OfType<FlowLayoutPanel>().Last().Controls.OfType<Label>().ToArray();
        return (labels[0].Text, labels[1].Text);
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private async Task DisconnectAsync()
    {
        var previous = _bridge;
        _bridge = null;
        _tools.Items.Clear();
        if (previous is null) return;
        try { await previous.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception ex) when (ex is McpException or HttpRequestException or IOException or TimeoutException or OperationCanceledException or InvalidOperationException)
        {
            if (!_closing) AddCard("Connection cleanup", "The previous connection did not close cleanly. Reconnect to start a new session.", Muted);
        }
    }

    private async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        _lifetime.Cancel();
        _operation?.Cancel();
        try
        {
            await _currentTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception) { /* Closing must not strand the window after a failed connection. */ }
        await DisconnectAsync();
        _allowClose = true;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_resourcesDisposed)
        {
            _resourcesDisposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
            _cardTitleFont.Dispose();
            _chat.Dispose();
        }
        base.Dispose(disposing);
    }
}
