using PhoneStore.Admin.Dialogs;
using PhoneStore.Admin.Management;
using PhoneStore.Admin.Styling;
using PhoneStore.Models.Management;

namespace PhoneStore.Admin.Controls;

public sealed class ProductsManagementControl : UserControl
{
    private readonly CatalogManagementGateway _gateway;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DataGridView _grid = ManagementUi.CreateGrid();
    private readonly TextBox _search = new()
    {
        Name = "ProductSearchInput", PlaceholderText = "Tên sản phẩm hoặc thương hiệu",
        Width = 220, MaxLength = 200, BorderStyle = BorderStyle.FixedSingle
    };
    private readonly ComboBox _brand = CreateSelector("ProductBrandFilter");
    private readonly ComboBox _category = CreateSelector("ProductCategoryFilter");
    private readonly ComboBox _active = CreateSelector("ProductStatusFilter");
    private readonly FlowLayoutPanel _filters;
    private readonly FlowLayoutPanel _actions;
    private readonly Label _status;
    private readonly Button _add;
    private readonly Button _edit;
    private readonly Button _toggle;
    private readonly Button _images;
    private bool _busy;
    private bool _loaded;
    private bool _lifetimeDisposed;

    public ProductsManagementControl(CatalogManagementGateway gateway)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        _gateway = gateway;
        SuspendLayout();
        Name = nameof(ProductsManagementControl);
        Font = new Font("Segoe UI", 10);
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = AdminTheme.Surface;
        Padding = new Padding(24);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6,
            Margin = Padding.Empty, Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var title = AdminTheme.CreateLabel("Danh sách sản phẩm", 18, true);
        title.Dock = DockStyle.Fill;
        title.Margin = new Padding(0, 0, 0, 8);
        var description = AdminTheme.CreateLabel("Quản lý thông tin, tồn kho và ảnh điện thoại trong PhoneStore.");
        description.ForeColor = AdminTheme.Muted;
        description.Dock = DockStyle.Fill;
        description.Margin = new Padding(0, 0, 0, 20);
        _filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            Margin = new Padding(0, 0, 0, 16)
        };
        _brand.DisplayMember = nameof(LookupOption.DisplayName);
        _category.DisplayMember = nameof(LookupOption.DisplayName);
        _active.DisplayMember = nameof(StatusOption.Name);
        _active.DataSource = new List<StatusOption>
        {
            new("Tất cả trạng thái", null), new("Đang hoạt động", true), new("Ngừng hoạt động", false)
        };
        _filters.Controls.Add(FilterField("Tìm kiếm", _search));
        _filters.Controls.Add(FilterField("Thương hiệu", _brand));
        _filters.Controls.Add(FilterField("Danh mục", _category));
        _filters.Controls.Add(FilterField("Trạng thái", _active));
        var filter = ManagementUi.CreateButton("Tìm kiếm");
        filter.Name = "ApplyProductFiltersButton";
        filter.Margin = new Padding(0, 23, 0, 0);
        filter.Click += async (_, _) => await RefreshAsync();
        _filters.Controls.Add(filter);
        _search.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                await RefreshAsync();
            }
        };
        _actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            Margin = new Padding(0, 0, 0, 16)
        };
        _add = ManagementUi.CreateButton("+ Thêm sản phẩm", true);
        _add.Name = "AddProductButton";
        _edit = ManagementUi.CreateButton("Chỉnh sửa");
        _edit.Name = "EditProductButton";
        _toggle = ManagementUi.CreateButton("Ngừng hoạt động");
        _toggle.Name = "ToggleProductActiveButton";
        _images = ManagementUi.CreateButton("Quản lý ảnh");
        _images.Name = "ManageProductImagesButton";
        var refresh = ManagementUi.CreateButton("Làm mới");
        refresh.Name = "RefreshProductsButton";
        _actions.Controls.AddRange([_add, _edit, _toggle, _images, refresh]);

        _grid.Name = "ProductsGrid";
        ManagementUi.AddColumn(_grid, nameof(ProductManagementRow.ProductId), "ID", 65);
        ManagementUi.AddColumn(_grid, nameof(ProductManagementRow.ProductName), "Tên sản phẩm", 260);
        ManagementUi.AddColumn(_grid, nameof(ProductManagementRow.BrandName), "Thương hiệu", 140);
        ManagementUi.AddColumn(_grid, nameof(ProductManagementRow.CategoryName), "Danh mục", 140);
        ManagementUi.AddColumn(_grid, nameof(ProductManagementRow.Price), "Giá (₫)", 150, "#,##0.##");
        ManagementUi.AddColumn(_grid, nameof(ProductManagementRow.StockQuantity), "Tồn kho", 100, "N0");
        ManagementUi.AddColumn(_grid, nameof(ProductManagementRow.RAM), "RAM", 100);
        ManagementUi.AddColumn(_grid, nameof(ProductManagementRow.Storage), "Bộ nhớ", 100);
        ManagementUi.AddColumn(_grid, nameof(ProductManagementRow.Status), "Trạng thái", 180);
        ManagementUi.AddColumn(_grid, nameof(ProductManagementRow.UpdatedAt), "Cập nhật", 170, "dd/MM/yyyy HH:mm");
        _grid.Columns[nameof(ProductManagementRow.Price)]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _grid.Columns[nameof(ProductManagementRow.StockQuantity)]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _grid.SelectionChanged += (_, _) => UpdateSelection();
        _grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await OpenEditAsync(); };
        _status = AdminTheme.CreateLabel("Đang tải danh sách…");
        _status.Name = "ProductsStatusLabel";
        _status.ForeColor = AdminTheme.Muted;
        _status.Dock = DockStyle.Fill;
        _status.Margin = new Padding(0, 12, 0, 0);
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(description, 0, 1);
        layout.Controls.Add(_filters, 0, 2);
        layout.Controls.Add(_actions, 0, 3);
        layout.Controls.Add(_grid, 0, 4);
        layout.Controls.Add(_status, 0, 5);
        Controls.Add(layout);
        _add.Click += async (_, _) => await OpenEditAsync(isNew: true);
        _edit.Click += async (_, _) => await OpenEditAsync();
        _toggle.Click += async (_, _) => await ToggleActiveAsync();
        _images.Click += async (_, _) => await OpenImagesAsync();
        refresh.Click += async (_, _) => await RefreshAsync(refreshOptions: true);
        Load += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            await RefreshAsync(refreshOptions: true);
        };
        UpdateSelection();
        ResumeLayout(true);
    }

    private sealed record StatusOption(string Name, bool? Value);
    private ProductManagementRow? Selected => _grid.CurrentRow?.DataBoundItem as ProductManagementRow;

    private static ComboBox CreateSelector(string name) => new()
    {
        Name = name, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList,
        IntegralHeight = false, MaxDropDownItems = 12
    };

    private static TableLayoutPanel FilterField(string caption, Control input)
    {
        var group = new TableLayoutPanel
        {
            AutoSize = true, ColumnCount = 1, RowCount = 2,
            Margin = new Padding(0, 0, 12, 10), Width = input.Width, MinimumSize = new Size(input.Width, 0)
        };
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        group.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        group.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = AdminTheme.CreateLabel(caption, 9, true);
        label.Dock = DockStyle.Fill;
        label.Margin = new Padding(0, 0, 0, 8);
        input.Dock = DockStyle.Top;
        input.Margin = Padding.Empty;
        input.AccessibleName = caption;
        group.Controls.Add(label, 0, 0);
        group.Controls.Add(input, 0, 1);
        return group;
    }

    private void UpdateSelection()
    {
        var selected = Selected;
        _edit.Enabled = !_busy && selected is not null;
        _toggle.Enabled = !_busy && selected is not null;
        _images.Enabled = !_busy && selected is not null;
        _toggle.Text = selected is { IsActive: false } ? "Kích hoạt" : "Ngừng hoạt động";
    }

    private Task RefreshAsync(bool refreshOptions = false) => RunAsync(async () =>
    {
        if (refreshOptions)
        {
            var options = await _gateway.WithProductsAsync(service => service.GetFilterOptionsAsync(_lifetime.Token));
            if (IsDisposed || _lifetime.IsCancellationRequested) return;
            LoadFilter(_brand, options.Brands, "Tất cả thương hiệu");
            LoadFilter(_category, options.Categories, "Tất cả danh mục");
        }
        await ReloadRowsAsync();
    });

    private static void LoadFilter(ComboBox selector, IReadOnlyList<LookupOption> options, string allCaption)
    {
        var previous = (selector.SelectedItem as LookupOption)?.Id ?? 0;
        var values = new List<LookupOption> { new(0, allCaption, true) };
        values.AddRange(options);
        selector.DataSource = values;
        var index = values.FindIndex(x => x.Id == previous);
        selector.SelectedIndex = index < 0 ? 0 : index;
    }

    private async Task ReloadRowsAsync(int? selectId = null)
    {
        var brandId = (_brand.SelectedItem as LookupOption)?.Id;
        var categoryId = (_category.SelectedItem as LookupOption)?.Id;
        var query = new ProductManagementQuery(_search.Text.Trim(), brandId > 0 ? brandId : null,
            categoryId > 0 ? categoryId : null, (_active.SelectedItem as StatusOption)?.Value);
        var rows = await _gateway.WithProductsAsync(service => service.ListAsync(query, _lifetime.Token));
        if (IsDisposed || _lifetime.IsCancellationRequested) return;
        _grid.DataSource = rows.ToList();
        if (selectId.HasValue)
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.DataBoundItem is ProductManagementRow product && product.ProductId == selectId)
                {
                    _grid.CurrentCell = row.Cells[0];
                    break;
                }
            }
        }
        _status.Text = rows.Count == 0
            ? "Không có sản phẩm phù hợp. Điều chỉnh bộ lọc hoặc thêm sản phẩm mới."
            : $"{rows.Count} sản phẩm";
    }

    private async Task OpenEditAsync(bool isNew = false)
    {
        var selected = Selected;
        if (_busy || (!isNew && selected is null)) return;
        var id = isNew ? (int?)null : selected!.ProductId;
        await RunAsync(async () =>
        {
            var data = await _gateway.WithProductsAsync(async service =>
            {
                var edit = id.HasValue ? await service.GetEditAsync(id.Value, _lifetime.Token) : null;
                var options = await service.GetOptionsAsync(id, _lifetime.Token);
                return (Edit: edit, Options: options);
            });
            if (IsDisposed || _lifetime.IsCancellationRequested) return;
            if (id.HasValue && data.Edit is null) { _status.Text = "Sản phẩm không còn tồn tại. Vui lòng làm mới danh sách."; return; }
            if (data.Options.Brands.Count == 0 || data.Options.Categories.Count == 0)
            {
                _status.Text = "Cần có thương hiệu và danh mục đang hoạt động trước khi thêm sản phẩm.";
                return;
            }
            using var dialog = new ProductEditDialog(data.Edit, data.Options, (edit, cancellationToken) =>
                _gateway.WithProductsWriteAsync(service => service.SaveAsync(id, edit, cancellationToken)));
            if (dialog.ShowDialog(FindForm()) == DialogResult.OK) await ReloadRowsAsync(id);
            else if (!IsDisposed) _status.Text = "Danh sách sản phẩm sẵn sàng.";
        });
    }

    private async Task ToggleActiveAsync()
    {
        var selected = Selected;
        if (_busy || selected is null) return;
        if (selected.IsActive && MessageBox.Show(FindForm(),
                $"Ngừng hoạt động sản phẩm “{selected.ProductName}”? Sản phẩm sẽ không xuất hiện trên cửa hàng.",
                "Ngừng hoạt động sản phẩm", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        await RunAsync(async () =>
        {
            var result = await _gateway.WithProductsWriteAsync(service =>
                service.SetActiveAsync(selected.ProductId, !selected.IsActive, _lifetime.Token));
            if (IsDisposed || _lifetime.IsCancellationRequested) return;
            if (result.Succeeded) await ReloadRowsAsync(selected.ProductId);
            else _status.Text = result.Error ?? "Không thể cập nhật trạng thái sản phẩm.";
        });
    }

    private async Task OpenImagesAsync()
    {
        var selected = Selected;
        if (_busy || selected is null) return;
        await RunAsync(async () =>
        {
            using var dialog = new ProductImagesDialog(selected.ProductId, selected.ProductName, _gateway);
            dialog.ShowDialog(FindForm());
            if (dialog.HasChanges) await ReloadRowsAsync(selected.ProductId);
            else if (!IsDisposed) _status.Text = "Danh sách sản phẩm sẵn sàng.";
        });
    }

    private async Task RunAsync(Func<Task> operation)
    {
        if (_busy || IsDisposed) return;
        _busy = true;
        _filters.Enabled = false;
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
                _filters.Enabled = true;
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
