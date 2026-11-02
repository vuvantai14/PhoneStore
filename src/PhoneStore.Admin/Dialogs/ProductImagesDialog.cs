using PhoneStore.Admin.Management;
using PhoneStore.Admin.Styling;
using PhoneStore.Business.Management;
using PhoneStore.Models.Management;

namespace PhoneStore.Admin.Dialogs;

public sealed class ProductImagesDialog : Form
{
    private readonly int _productId;
    private readonly CatalogManagementGateway _gateway;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DataGridView _grid = ManagementUi.CreateGrid();
    private readonly FlowLayoutPanel _actions;
    private readonly Label _status;
    private readonly Button _add;
    private readonly Button _remove;
    private readonly Button _primary;
    private readonly Button _applyOrder;
    private readonly NumericUpDown _order;
    private bool _busy;
    private bool _lifetimeDisposed;

    public bool HasChanges { get; private set; }

    public ProductImagesDialog(int productId, string productName, CatalogManagementGateway gateway)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        _productId = productId;
        _gateway = gateway;
        SuspendLayout();
        Name = nameof(ProductImagesDialog);
        Text = "Quản lý ảnh sản phẩm";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.White;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(680, 500);
        ClientSize = new Size(880, 600);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6,
            Padding = new Padding(24), Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var title = AdminTheme.CreateLabel(productName, 18, true);
        title.AutoSize = false;
        title.AutoEllipsis = true;
        title.Height = 36;
        title.Dock = DockStyle.Fill;
        title.Margin = new Padding(0, 0, 0, 8);
        var description = AdminTheme.CreateLabel("Chọn ảnh JPG, JPEG, PNG hoặc WEBP. Mỗi sản phẩm có tối đa một ảnh chính.");
        description.ForeColor = AdminTheme.Muted;
        description.Dock = DockStyle.Fill;
        description.Margin = new Padding(0, 0, 0, 16);
        _actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            Margin = new Padding(0, 0, 0, 16)
        };
        _add = ManagementUi.CreateButton("+ Thêm ảnh", true);
        _add.Name = "AddImageButton";
        _remove = ManagementUi.CreateButton("Xóa ảnh");
        _remove.Name = "RemoveImageButton";
        _primary = ManagementUi.CreateButton("Đặt ảnh chính");
        _primary.Name = "SetPrimaryImageButton";
        var orderLabel = AdminTheme.CreateLabel("Thứ tự:");
        orderLabel.Margin = new Padding(8, 14, 8, 0);
        _order = new NumericUpDown
        {
            Name = "ImageDisplayOrderInput", Width = 80, Minimum = 0, Maximum = int.MaxValue,
            TextAlign = HorizontalAlignment.Right, Margin = new Padding(0, 9, 8, 0), AccessibleName = "Thứ tự ảnh"
        };
        _applyOrder = ManagementUi.CreateButton("Áp dụng");
        _applyOrder.Name = "ApplyImageOrderButton";
        var orderActions = new FlowLayoutPanel
        {
            AutoSize = true, WrapContents = false, Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        orderActions.Controls.AddRange([orderLabel, _order, _applyOrder]);
        _actions.Controls.AddRange([_add, _primary, _remove, orderActions]);

        _grid.Name = "ProductImagesGrid";
        ManagementUi.AddColumn(_grid, nameof(ProductImageRow.ImageId), "ID", 65);
        ManagementUi.AddColumn(_grid, nameof(ProductImageRow.ImageUrl), "Đường dẫn ảnh", 440);
        ManagementUi.AddColumn(_grid, nameof(ProductImageRow.PrimaryLabel), "Ảnh chính", 145);
        ManagementUi.AddColumn(_grid, nameof(ProductImageRow.DisplayOrder), "Thứ tự", 90);
        _grid.Columns[nameof(ProductImageRow.ImageUrl)]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        _grid.Columns[nameof(ProductImageRow.ImageUrl)]!.MinimumWidth = 240;
        _grid.SelectionChanged += (_, _) => UpdateSelection();
        _status = AdminTheme.CreateLabel("", 10);
        _status.Name = "ImageStatusLabel";
        _status.ForeColor = AdminTheme.Muted;
        _status.Dock = DockStyle.Fill;
        _status.Margin = new Padding(0, 12, 0, 0);
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, Margin = new Padding(0, 16, 0, 0)
        };
        var close = ManagementUi.CreateButton("Đóng");
        close.Name = "CloseProductImagesButton";
        close.DialogResult = DialogResult.Cancel;
        footer.Controls.Add(close);
        CancelButton = close;
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(description, 0, 1);
        layout.Controls.Add(_actions, 0, 2);
        layout.Controls.Add(_grid, 0, 3);
        layout.Controls.Add(_status, 0, 4);
        layout.Controls.Add(footer, 0, 5);
        Controls.Add(layout);
        _add.Click += async (_, _) => await AddAsync();
        _remove.Click += async (_, _) => await RemoveAsync();
        _primary.Click += async (_, _) => await ChangeAsync((service, image, token) =>
            service.SetPrimaryImageAsync(_productId, image.ImageId, token));
        _applyOrder.Click += async (_, _) => await ChangeAsync((service, image, token) =>
            service.SetImageOrderAsync(_productId, image.ImageId, decimal.ToInt32(_order.Value), token));
        Shown += async (_, _) => await RunAsync(async () => await ReloadAsync());
        FormClosing += (_, _) => _lifetime.Cancel();
        UpdateSelection();
        ResumeLayout(true);
    }

    private ProductImageRow? Selected => _grid.CurrentRow?.DataBoundItem as ProductImageRow;

    private void UpdateSelection()
    {
        var selected = Selected;
        _remove.Enabled = !_busy && selected is not null;
        _primary.Enabled = !_busy && selected is { IsPrimary: false };
        _order.Enabled = !_busy && selected is not null;
        _applyOrder.Enabled = !_busy && selected is not null;
        if (selected is not null) _order.Value = Math.Clamp(selected.DisplayOrder, 0, int.MaxValue);
    }

    private async Task ReloadAsync(int? selectId = null)
    {
        var rows = await _gateway.WithProductsAsync(service => service.GetImagesAsync(_productId, _lifetime.Token));
        if (IsDisposed || _lifetime.IsCancellationRequested) return;
        _grid.DataSource = rows.ToList();
        if (selectId.HasValue)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.DataBoundItem is ProductImageRow image && image.ImageId == selectId)
                {
                    _grid.CurrentCell = row.Cells[0];
                    break;
                }
            }
        }
        _status.Text = rows.Count == 0 ? "Chưa có ảnh. Chọn Thêm ảnh để bắt đầu." : $"{rows.Count} ảnh sản phẩm";
    }

    private async Task AddAsync()
    {
        if (_busy) return;
        using var selection = new OpenFileDialog
        {
            Title = "Chọn ảnh sản phẩm", Multiselect = false, CheckFileExists = true,
            Filter = "Ảnh sản phẩm (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
            RestoreDirectory = true
        };
        if (selection.ShowDialog(this) != DialogResult.OK) return;
        await RunAsync(async () =>
        {
            var result = await _gateway.WithProductsAsync(service =>
                service.AddImageAsync(_productId, selection.FileName, _lifetime.Token));
            await HandleResultAsync(result, result.Id);
        });
    }

    private async Task RemoveAsync()
    {
        var image = Selected;
        if (_busy || image is null) return;
        if (MessageBox.Show(this, "Xóa ảnh đã chọn khỏi sản phẩm?", "Xóa ảnh sản phẩm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        await RunAsync(async () =>
        {
            var result = await _gateway.WithProductsAsync(service =>
                service.RemoveImageAsync(_productId, image.ImageId, _lifetime.Token));
            await HandleResultAsync(result);
        });
    }

    private async Task ChangeAsync(Func<IProductManagementService, ProductImageRow, CancellationToken, Task<ManagementResult>> operation)
    {
        var image = Selected;
        if (_busy || image is null) return;
        await RunAsync(async () =>
        {
            var result = await _gateway.WithProductsAsync(service => operation(service, image, _lifetime.Token));
            await HandleResultAsync(result, image.ImageId);
        });
    }

    private async Task HandleResultAsync(ManagementResult result, int? selectId = null)
    {
        if (IsDisposed || _lifetime.IsCancellationRequested) return;
        if (!result.Succeeded) { _status.Text = result.Error ?? "Không thể cập nhật ảnh sản phẩm."; return; }
        HasChanges = true;
        await ReloadAsync(selectId);
        if (!IsDisposed && result.Notice is not null) _status.Text = result.Notice;
    }

    private async Task RunAsync(Func<Task> operation)
    {
        if (_busy || IsDisposed) return;
        _busy = true;
        _actions.Enabled = false;
        _grid.Enabled = false;
        _status.Text = "Đang xử lý…";
        try { await operation(); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { if (!IsDisposed) _status.Text = ManagementUi.ErrorMessage(ex); }
        finally
        {
            if (!IsDisposed)
            {
                _busy = false;
                _actions.Enabled = true;
                _grid.Enabled = true;
                UpdateSelection();
            }
        }
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
