using PhoneStore.Admin.Styling;
using PhoneStore.Models.Management;

namespace PhoneStore.Admin.Dialogs;

public sealed class LookupEditDialog : Form
{
    private readonly Func<LookupEdit, Task<ManagementResult>> save;
    private readonly TextBox nameInput = new() { Name = "LookupNameInput", MaxLength = 100 };
    private readonly TextBox descriptionInput = new()
    {
        Name = "LookupDescriptionInput", MaxLength = 500, Multiline = true,
        ScrollBars = ScrollBars.Vertical, AcceptsReturn = true
    };
    private readonly CheckBox activeInput = new() { Name = "LookupActiveInput", Text = "Đang hoạt động", AutoSize = true };
    private readonly Label status = new() { Name = "LookupEditStatus", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button saveButton = new() { Name = "SaveLookupButton", Text = "Lưu", AutoSize = true };
    private readonly Button cancelButton = new() { Name = "CancelLookupButton", Text = "Hủy", AutoSize = true, DialogResult = DialogResult.Cancel };
    private readonly string kindName;
    private bool busy;

    public LookupEditDialog(LookupKind kind, LookupEdit? edit, Func<LookupEdit, Task<ManagementResult>> save)
    {
        SuspendLayout();
        this.save = save;
        kindName = kind == LookupKind.Brand ? "thương hiệu" : "danh mục";
        Name = nameof(LookupEditDialog);
        Text = (edit is null ? "Thêm " : "Chỉnh sửa ") + kindName + " · PhoneStore";
        Font = new Font("Segoe UI", 10);
        ForeColor = AdminTheme.Navy;
        BackColor = Color.White;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(580, 540);
        MinimumSize = new Size(540, 520);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        nameInput.Text = edit?.Name ?? "";
        descriptionInput.Text = edit?.Description ?? "";
        activeInput.Checked = edit?.IsActive ?? true;
        nameInput.AccessibleName = "Tên " + kindName;
        descriptionInput.AccessibleName = "Mô tả";

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(24), Margin = Padding.Empty,
            ColumnCount = 1, RowCount = 6
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(layout);

        var title = AdminTheme.CreateLabel((edit is null ? "Thêm " : "Chỉnh sửa ") + kindName, 19, true);
        title.Dock = DockStyle.Fill;
        title.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(CreateField("Tên " + kindName + " *", nameInput), 0, 1);
        layout.Controls.Add(CreateField("Mô tả (tùy chọn)", descriptionInput), 0, 2);
        activeInput.Anchor = AnchorStyles.Left;
        activeInput.Margin = new Padding(0, 8, 0, 0);
        layout.Controls.Add(activeInput, 0, 3);
        status.ForeColor = Color.FromArgb(165, 47, 58);
        status.AccessibleName = "Thông báo lưu dữ liệu";
        layout.Controls.Add(status, 0, 4);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, Margin = Padding.Empty, Padding = Padding.Empty
        };
        AdminTheme.StyleButton(saveButton, primary: true);
        AdminTheme.StyleButton(cancelButton);
        saveButton.MinimumSize = cancelButton.MinimumSize = new Size(104, 44);
        saveButton.Margin = Padding.Empty;
        cancelButton.Margin = new Padding(0, 0, 12, 0);
        actions.Controls.Add(saveButton);
        actions.Controls.Add(cancelButton);
        layout.Controls.Add(actions, 0, 5);
        saveButton.Click += async (_, _) => await SaveAsync();
        AcceptButton = saveButton;
        CancelButton = cancelButton;
        Shown += (_, _) => nameInput.Focus();
        FormClosing += (_, e) => { if (busy) e.Cancel = true; };
        ResumeLayout(true);
    }

    private static TableLayoutPanel CreateField(string labelText, TextBox input)
    {
        var field = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Margin = new Padding(0, 0, 0, 12), Padding = Padding.Empty
        };
        field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        field.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        field.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var label = AdminTheme.CreateLabel(labelText, 10, true);
        label.Dock = DockStyle.Fill;
        field.Controls.Add(label, 0, 0);
        var frame = new Panel
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10, 8, 10, 8), BackColor = Color.White
        };
        input.BorderStyle = BorderStyle.None;
        input.Dock = DockStyle.Fill;
        input.Font = new Font("Segoe UI", 11);
        input.ForeColor = AdminTheme.Navy;
        input.Enter += (_, _) => frame.BackColor = input.BackColor = Color.FromArgb(237, 243, 255);
        input.Leave += (_, _) => frame.BackColor = input.BackColor = Color.White;
        frame.Controls.Add(input);
        field.Controls.Add(frame, 0, 1);
        return field;
    }

    private async Task SaveAsync()
    {
        if (busy) return;
        var name = nameInput.Text.Trim();
        if (name.Length == 0)
        {
            status.Text = "Vui lòng nhập tên " + kindName + ".";
            nameInput.Focus();
            return;
        }
        SetBusy(true);
        status.Text = "Đang lưu…";
        try
        {
            var result = await save(new LookupEdit(name, descriptionInput.Text.Trim(), activeInput.Checked));
            if (!result.Succeeded)
            {
                status.Text = result.Error ?? "Không thể lưu dữ liệu. Vui lòng thử lại.";
                return;
            }
            SetBusy(false);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            if (!IsDisposed) status.Text = ManagementUi.ErrorMessage(exception);
        }
        finally
        {
            if (!IsDisposed) SetBusy(false);
        }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        nameInput.ReadOnly = descriptionInput.ReadOnly = value;
        activeInput.Enabled = saveButton.Enabled = cancelButton.Enabled = !value;
        saveButton.Text = value ? "Đang lưu…" : "Lưu";
        UseWaitCursor = value;
    }
}
