using PhoneStore.Admin.Styling;
using PhoneStore.Models.Management;

namespace PhoneStore.Admin.Dialogs;

public sealed class CustomerEditDialog : Form
{
    private readonly CustomerManagementRow customer;
    private readonly Func<CustomerEdit, Task<ManagementResult>> save;
    private readonly TextBox fullName = new() { MaxLength = 100 };
    private readonly TextBox phone = new() { MaxLength = 20 };
    private readonly TextBox address = new() { MaxLength = 255, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly Label status = AdminTheme.CreateLabel("");
    private readonly Button submit = ManagementUi.CreateButton("Lưu thay đổi", true);

    public CustomerEditDialog(CustomerManagementRow customer, Func<CustomerEdit, Task<ManagementResult>> save)
    {
        this.customer = customer;
        this.save = save;
        Text = "Chi tiết khách hàng"; Name = nameof(CustomerEditDialog);
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        MinimizeBox = MaximizeBox = false; ClientSize = new Size(620, 570); MinimumSize = new Size(560, 520);
        Font = new Font("Segoe UI", 10); BackColor = Color.White;
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9, Padding = new Padding(28) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 7; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(AdminTheme.CreateLabel("Thông tin khách hàng", 19, true), 0, 0);
        layout.Controls.Add(ReadOnlyField("Email", customer.Email), 0, 1);
        layout.Controls.Add(ReadOnlyField("Trạng thái", customer.Status), 0, 2);
        layout.Controls.Add(ReadOnlyField("Ngày tạo (UTC)", customer.CreatedAt.ToString("dd/MM/yyyy HH:mm")), 0, 3);
        layout.Controls.Add(Field("Họ tên *", fullName, 46), 0, 4);
        layout.Controls.Add(Field("Số điện thoại", phone, 46), 0, 5);
        layout.Controls.Add(Field("Địa chỉ", address, 92), 0, 6);
        status.ForeColor = Color.FromArgb(165, 47, 58); status.Dock = DockStyle.Fill;
        layout.Controls.Add(status, 0, 7);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var cancel = ManagementUi.CreateButton("Đóng"); cancel.DialogResult = DialogResult.Cancel;
        footer.Controls.Add(submit); footer.Controls.Add(cancel); layout.Controls.Add(footer, 0, 8);
        Controls.Add(layout); AcceptButton = submit; CancelButton = cancel;
        fullName.Text = customer.FullName; phone.Text = customer.Phone ?? ""; address.Text = customer.Address ?? "";
        submit.Click += async (_, _) => await SaveAsync();
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(fullName.Text)) { status.Text = "Vui lòng nhập họ tên khách hàng."; fullName.Focus(); return; }
        submit.Enabled = false; status.Text = "Đang lưu…";
        try
        {
            var result = await save(new CustomerEdit(fullName.Text, phone.Text, address.Text));
            if (result.Succeeded) { DialogResult = DialogResult.OK; Close(); }
            else status.Text = result.Error ?? "Không thể lưu thay đổi.";
        }
        catch (Exception exception) { status.Text = ManagementUi.ErrorMessage(exception); }
        finally { if (!IsDisposed) submit.Enabled = true; }
    }

    private static Control ReadOnlyField(string label, string value)
    {
        var text = new TextBox { Text = value, ReadOnly = true, BackColor = AdminTheme.Surface, BorderStyle = BorderStyle.FixedSingle };
        return Field(label, text, 44);
    }

    private static Control Field(string label, Control input, int height)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 10, 0, 0) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        var caption = AdminTheme.CreateLabel(label, 9.5f, true); caption.Dock = DockStyle.Fill; caption.Margin = new Padding(0, 0, 0, 6);
        input.Dock = DockStyle.Fill; input.Margin = Padding.Empty;
        panel.Controls.Add(caption, 0, 0); panel.Controls.Add(input, 0, 1); return panel;
    }
}
