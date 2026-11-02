using PhoneStore.Admin.Styling;
using PhoneStore.Models.Management;

namespace PhoneStore.Admin.Dialogs;

public sealed class ProductEditDialog : Form
{
    private readonly Func<ProductEdit, CancellationToken, Task<ManagementResult>> _save;
    private readonly int? _originalStockQuantity;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TableLayoutPanel _fields;
    private readonly Label _error;
    private readonly Button _saveButton;
    private readonly TextBox _name = CreateTextBox("ProductNameInput", 200);
    private readonly ComboBox _brand = CreateSelector("BrandSelector");
    private readonly ComboBox _category = CreateSelector("CategorySelector");
    private readonly NumericUpDown _price = CreateNumber("PriceInput", 9999999999999999.99m, 2);
    private readonly NumericUpDown _stock = CreateNumber("StockInput", int.MaxValue);
    private readonly TextBox _ram = CreateTextBox("RAMInput", 50);
    private readonly TextBox _storage = CreateTextBox("StorageInput", 50);
    private readonly TextBox _chip = CreateTextBox("ChipInput", 150);
    private readonly TextBox _screen = CreateTextBox("ScreenInput", 200);
    private readonly TextBox _camera = CreateTextBox("CameraInput", 250);
    private readonly TextBox _battery = CreateTextBox("BatteryInput", 100);
    private readonly TextBox _os = CreateTextBox("OperatingSystemInput", 100);
    private readonly TextBox _description = CreateTextBox("DescriptionInput", 0);
    private readonly CheckBox _active = new()
    {
        Name = "ActiveInput", Text = "Đang hoạt động", Checked = true,
        AutoSize = true, MinimumSize = new Size(0, 40)
    };
    private bool _saving;
    private bool _lifetimeDisposed;

    public ProductEditDialog(ProductEdit? edit, ProductEditOptions options,
        Func<ProductEdit, CancellationToken, Task<ManagementResult>> save)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(save);
        _save = save;
        _originalStockQuantity = edit?.OriginalStockQuantity;
        SuspendLayout();
        Name = nameof(ProductEditDialog);
        Text = edit is null ? "Thêm sản phẩm" : "Chỉnh sửa sản phẩm";
        Font = new Font("Segoe UI", 10);
        BackColor = Color.White;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        MinimumSize = new Size(620, 520);
        ClientSize = new Size(760, 720);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            Padding = new Padding(24), Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var introduction = AdminTheme.CreateLabel("Thông tin bắt buộc được đánh dấu *. Giá phải lớn hơn 0.", 10);
        introduction.ForeColor = AdminTheme.Muted;
        introduction.Dock = DockStyle.Fill;
        introduction.Margin = new Padding(0, 0, 0, 20);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
        _fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 0,
            Padding = new Padding(0, 0, 16, 0), Margin = Padding.Empty
        };
        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        AddRow(Field("Tên sản phẩm *", _name), fullWidth: true);
        AddRow(Field("Thương hiệu *", _brand), Field("Danh mục *", _category));
        AddRow(Field("Giá (₫) *", _price), Field("Số lượng tồn kho *", _stock));
        AddRow(Field("RAM", _ram), Field("Bộ nhớ", _storage));
        AddRow(Field("Chip", _chip), Field("Màn hình", _screen));
        AddRow(Field("Camera", _camera), Field("Pin", _battery));
        AddRow(Field("Hệ điều hành", _os), Field("Trạng thái", _active));
        _description.Multiline = true;
        _description.ScrollBars = ScrollBars.Vertical;
        _description.Height = 116;
        AddRow(Field("Mô tả", _description), fullWidth: true);
        scroll.Controls.Add(_fields);

        _error = AdminTheme.CreateLabel("", 10);
        _error.Name = "ValidationMessage";
        _error.ForeColor = Color.FromArgb(178, 42, 42);
        _error.Dock = DockStyle.Fill;
        _error.Margin = new Padding(0, 12, 0, 0);
        _error.MaximumSize = new Size(0, 0);
        _error.AccessibleRole = AccessibleRole.Alert;
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, Margin = new Padding(0, 16, 0, 0)
        };
        _saveButton = MakeButton("Lưu sản phẩm", true);
        _saveButton.Name = "SaveProductButton";
        _saveButton.Click += async (_, _) => await SaveAsync();
        var cancel = MakeButton("Hủy");
        cancel.Name = "CancelProductButton";
        cancel.DialogResult = DialogResult.Cancel;
        actions.Controls.Add(_saveButton);
        actions.Controls.Add(cancel);
        layout.Controls.Add(introduction, 0, 0);
        layout.Controls.Add(scroll, 0, 1);
        layout.Controls.Add(_error, 0, 2);
        layout.Controls.Add(actions, 0, 3);
        Controls.Add(layout);
        AcceptButton = _saveButton;
        CancelButton = cancel;
        _brand.DisplayMember = nameof(LookupOption.DisplayName);
        _category.DisplayMember = nameof(LookupOption.DisplayName);
        _brand.DataSource = options.Brands.ToList();
        _category.DataSource = options.Categories.ToList();
        _brand.SelectedIndex = -1;
        _category.SelectedIndex = -1;
        if (edit is not null) LoadEdit(edit);
        FormClosing += (_, _) => _lifetime.Cancel();
        ResumeLayout(true);
    }

    private void AddRow(Control left, Control? right = null, bool fullWidth = false)
    {
        var row = _fields.RowCount++;
        _fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _fields.Controls.Add(left, 0, row);
        if (fullWidth) _fields.SetColumnSpan(left, 2);
        else if (right is not null) _fields.Controls.Add(right, 1, row);
    }

    private static TableLayoutPanel Field(string caption, Control input)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2,
            Margin = new Padding(0, 0, 12, 18), Padding = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = AdminTheme.CreateLabel(caption, 10, true);
        label.Dock = DockStyle.Fill;
        label.Margin = new Padding(0, 0, 0, 8);
        input.Dock = DockStyle.Top;
        input.Margin = Padding.Empty;
        input.AccessibleName = caption;
        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(input, 0, 1);
        return panel;
    }

    private static TextBox CreateTextBox(string name, int maximum) => new()
    {
        Name = name, MaxLength = maximum, BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Segoe UI", 10), ForeColor = AdminTheme.Navy
    };

    private static ComboBox CreateSelector(string name) => new()
    {
        Name = name, DropDownStyle = ComboBoxStyle.DropDownList,
        Font = new Font("Segoe UI", 10), ForeColor = AdminTheme.Navy, IntegralHeight = false, MaxDropDownItems = 12
    };

    private static NumericUpDown CreateNumber(string name, decimal maximum, int decimals = 0) => new()
    {
        Name = name, Minimum = 0, Maximum = maximum, DecimalPlaces = decimals,
        ThousandsSeparator = true, TextAlign = HorizontalAlignment.Right,
        Font = new Font("Segoe UI", 10), ForeColor = AdminTheme.Navy
    };

    private static Button MakeButton(string text, bool primary = false)
    {
        var button = new Button { Text = text, AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
        AdminTheme.StyleButton(button, primary);
        return button;
    }

    private void LoadEdit(ProductEdit edit)
    {
        _name.Text = edit.ProductName;
        SelectOption(_brand, edit.BrandId);
        SelectOption(_category, edit.CategoryId);
        _price.Value = Math.Clamp(edit.Price, _price.Minimum, _price.Maximum);
        _stock.Value = Math.Clamp(edit.StockQuantity, 0, int.MaxValue);
        _ram.Text = edit.RAM;
        _storage.Text = edit.Storage;
        _chip.Text = edit.Chip;
        _screen.Text = edit.Screen;
        _camera.Text = edit.Camera;
        _battery.Text = edit.Battery;
        _os.Text = edit.OperatingSystem;
        _description.Text = edit.Description;
        _active.Checked = edit.IsActive;
    }

    private static void SelectOption(ComboBox selector, int id)
    {
        for (var i = 0; i < selector.Items.Count; i++)
        {
            if (selector.Items[i] is LookupOption option && option.Id == id)
            {
                selector.SelectedIndex = i;
                return;
            }
        }
    }

    private async Task SaveAsync()
    {
        if (_saving) return;
        if (string.IsNullOrWhiteSpace(_name.Text)) { ShowValidation("Vui lòng nhập tên sản phẩm.", _name); return; }
        if (_brand.SelectedItem is not LookupOption brand) { ShowValidation("Vui lòng chọn thương hiệu.", _brand); return; }
        if (_category.SelectedItem is not LookupOption category) { ShowValidation("Vui lòng chọn danh mục.", _category); return; }
        if (_price.Value <= 0) { ShowValidation("Giá sản phẩm phải lớn hơn 0.", _price); return; }
        var edit = new ProductEdit
        {
            ProductName = _name.Text.Trim(), BrandId = brand.Id, CategoryId = category.Id,
            Price = _price.Value, StockQuantity = decimal.ToInt32(_stock.Value),
            OriginalStockQuantity = _originalStockQuantity,
            RAM = _ram.Text.Trim(), Storage = _storage.Text.Trim(), Chip = _chip.Text.Trim(),
            Screen = _screen.Text.Trim(), Camera = _camera.Text.Trim(), Battery = _battery.Text.Trim(),
            OperatingSystem = _os.Text.Trim(), Description = _description.Text.Trim(), IsActive = _active.Checked
        };
        _saving = true;
        _fields.Enabled = false;
        _saveButton.Enabled = false;
        _error.Text = "Đang lưu sản phẩm…";
        try
        {
            var result = await _save(edit, _lifetime.Token);
            if (IsDisposed || _lifetime.IsCancellationRequested) return;
            if (result.Succeeded) { DialogResult = DialogResult.OK; Close(); }
            else _error.Text = result.Error ?? "Không thể lưu sản phẩm. Vui lòng thử lại.";
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception)
        {
            if (!IsDisposed) _error.Text = "Không thể lưu sản phẩm. Kiểm tra kết nối dữ liệu rồi thử lại.";
        }
        finally
        {
            if (!IsDisposed)
            {
                _saving = false;
                _fields.Enabled = true;
                _saveButton.Enabled = true;
            }
        }
    }

    private void ShowValidation(string message, Control input)
    {
        _error.Text = message;
        input.Focus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_lifetimeDisposed)
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            _lifetimeDisposed = true;
        }
        base.Dispose(disposing);
    }
}
