namespace AlexaDesktop;

internal sealed class WorkspaceDialog : Form
{
    private readonly TextBox _folder = new() { Dock = DockStyle.Fill, Text = "." };
    private readonly CheckBox _openFolder = new() { Text = "Open this folder", Checked = true, AutoSize = true };
    private readonly CheckBox _openNotepad = new() { Text = "Open Windows Notepad", AutoSize = true };
    internal string RelativeFolder => _folder.Text;
    internal bool OpenFolder => _openFolder.Checked;
    internal bool OpenNotepad => _openNotepad.Checked;

    internal void FillForCheck(string folder, bool openFolder, bool openNotepad)
    { _folder.Text = folder; _openFolder.Checked = openFolder; _openNotepad.Checked = openNotepad; }

    public WorkspaceDialog(string name)
    {
        Text = "Save workspace · " + name;
        ClientSize = new Size(490, 260);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 6 };
        foreach (var height in new[] { 48, 28, 36, 30, 30, 40 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.Controls.Add(new Label { Text = "Choose actions to save with the name and summary. Saving does not launch anything.", Dock = DockStyle.Fill }, 0, 0);
        layout.Controls.Add(new Label { Text = "Folder relative to the approved root ('.' = root)", AutoSize = true }, 0, 1);
        layout.Controls.Add(_folder, 0, 2);
        layout.Controls.Add(_openFolder, 0, 3);
        layout.Controls.Add(_openNotepad, 0, 4);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = new Button { Text = "Save", AutoSize = true };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => {
            if (!OpenFolder && !OpenNotepad) { MessageBox.Show(this, "Choose at least one action.", "Workspace"); return; }
            DialogResult = DialogResult.OK;
        };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 5);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }
}
