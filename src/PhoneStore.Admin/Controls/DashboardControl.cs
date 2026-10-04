using PhoneStore.Admin.Styling;

namespace PhoneStore.Admin.Controls;

public sealed class DashboardControl : UserControl
{
    public DashboardControl()
    {
        SuspendLayout();
        Name = nameof(DashboardControl);
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = AdminTheme.Surface;
        Padding = new Padding(32);
        AutoScroll = true;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < layout.RowCount; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        var eyebrow = AdminTheme.CreateLabel("PHONESTORE ADMIN", 9, true);
        eyebrow.ForeColor = AdminTheme.Primary;
        eyebrow.Margin = new Padding(0, 0, 0, 12);
        var heading = AdminTheme.CreateLabel("Chào mừng đến với PhoneStore Admin", 21, true);
        heading.Dock = DockStyle.Fill;
        heading.Margin = new Padding(0, 0, 0, 12);
        var introduction = AdminTheme.CreateLabel("Theo dõi hoạt động của PhoneStore.", 11);
        introduction.ForeColor = AdminTheme.Muted;
        introduction.Dock = DockStyle.Fill;

        var card = AdminTheme.CreateCard();
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 28, 0, 0);
        var cardContent = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        cardContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < cardContent.RowCount; i++)
        {
            cardContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
        var cardHeading = AdminTheme.CreateLabel("Không gian quản trị", 14, true);
        cardHeading.Dock = DockStyle.Fill;
        cardHeading.Margin = new Padding(0, 0, 0, 12);
        var guidance = AdminTheme.CreateLabel(
            "Sử dụng thanh điều hướng bên trái để mở các khu vực quản lý.", 10.5f);
        guidance.Dock = DockStyle.Fill;
        guidance.ForeColor = AdminTheme.Muted;
        guidance.Margin = new Padding(0, 0, 0, 8);
        var scope = AdminTheme.CreateLabel(
            "Các chức năng quản lý sẽ được triển khai ở bước tiếp theo.", 10.5f);
        scope.Dock = DockStyle.Fill;
        scope.ForeColor = AdminTheme.Muted;
        cardContent.Controls.Add(cardHeading, 0, 0);
        cardContent.Controls.Add(guidance, 0, 1);
        cardContent.Controls.Add(scope, 0, 2);
        card.Controls.Add(cardContent);

        layout.Controls.Add(eyebrow, 0, 0);
        layout.Controls.Add(heading, 0, 1);
        layout.Controls.Add(introduction, 0, 2);
        layout.Controls.Add(card, 0, 3);
        Controls.Add(layout);
        ResumeLayout(true);
    }
}
