using PhoneStore.Admin.Styling;

namespace PhoneStore.Admin.Controls;

public sealed class ModulePlaceholderControl : UserControl
{
    public ModulePlaceholderControl(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        SuspendLayout();
        Name = nameof(ModulePlaceholderControl);
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = AdminTheme.Surface;
        Padding = new Padding(32);
        AutoScroll = true;

        var card = AdminTheme.CreateCard();
        card.Dock = DockStyle.Top;
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var heading = AdminTheme.CreateLabel(title, 20, true);
        heading.Dock = DockStyle.Fill;
        heading.Margin = new Padding(0, 0, 0, 14);
        var description = AdminTheme.CreateLabel(
            "Chức năng sẽ được triển khai ở bước tiếp theo.", 11);
        description.Dock = DockStyle.Fill;
        description.ForeColor = AdminTheme.Muted;
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(description, 0, 1);
        card.Controls.Add(layout);
        Controls.Add(card);
        ResumeLayout(true);
    }
}
