namespace PhoneStore.Admin.Styling;

public static class AdminTheme
{
    public static readonly Color Primary = Color.FromArgb(21, 89, 214);
    public static readonly Color Navy = Color.FromArgb(20, 36, 60);
    public static readonly Color Muted = Color.FromArgb(99, 112, 134);
    public static readonly Color Surface = Color.FromArgb(245, 247, 251);
    public static readonly Color Border = Color.FromArgb(225, 231, 239);

    public static Label CreateLabel(string text, float size = 10, bool bold = false)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = Navy,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            UseMnemonic = false
        };
    }

    public static void StyleButton(Button button, bool primary = false)
    {
        button.Font = new Font("Segoe UI", 10, FontStyle.Regular);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = primary
            ? Color.FromArgb(16, 70, 173)
            : Surface;
        button.FlatAppearance.MouseDownBackColor = primary
            ? Color.FromArgb(12, 58, 150)
            : Color.FromArgb(232, 238, 248);
        button.ForeColor = primary ? Color.White : Navy;
        button.BackColor = primary ? Primary : Color.White;
        button.UseVisualStyleBackColor = false;
        button.MinimumSize = new Size(0, 44);
        button.Padding = new Padding(12, 6, 12, 6);
        button.Cursor = Cursors.Hand;
    }

    public static Panel CreateCard()
    {
        var panel = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.White,
            Padding = new Padding(24),
            Margin = Padding.Empty
        };
        panel.Paint += (_, e) =>
        {
            if (panel.Width > 1 && panel.Height > 1)
            {
                using var pen = new Pen(Border);
                e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            }
        };
        return panel;
    }
}
