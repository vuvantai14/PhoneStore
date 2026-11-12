using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace PhoneStore.Admin.Styling;

public static class AdminTheme
{
    // Design Tokens - Colors
    public static readonly Color Primary = Color.FromArgb(21, 89, 214);       // #1559D6
    public static readonly Color PrimaryDark = Color.FromArgb(16, 70, 173);   // #1046AD
    public static readonly Color PrimaryLight = Color.FromArgb(235, 242, 254); // #EBF2FE
    public static readonly Color Navy = Color.FromArgb(20, 36, 60);          // #14243C
    public static readonly Color Secondary = Color.FromArgb(90, 115, 142);    // #5A738E
    public static readonly Color Muted = Color.FromArgb(90, 115, 142);        // #5A738E
    public static readonly Color Surface = Color.FromArgb(245, 247, 251);     // #F5F7FB
    public static readonly Color SurfaceWhite = Color.White;
    public static readonly Color Border = Color.FromArgb(217, 226, 238);      // #D9E2EE
    public static readonly Color BorderLight = Color.FromArgb(235, 240, 247);

    // Status Badges
    public static readonly Color PendingBg = Color.FromArgb(254, 243, 199);     // #FEF3C7
    public static readonly Color PendingText = Color.FromArgb(180, 83, 9);       // #B45309
    public static readonly Color ConfirmedBg = Color.FromArgb(224, 242, 254);   // #E0F2FE
    public static readonly Color ConfirmedText = Color.FromArgb(3, 105, 161);    // #0369A1
    public static readonly Color ShippingBg = Color.FromArgb(237, 233, 254);    // #EDE9FE
    public static readonly Color ShippingText = Color.FromArgb(109, 40, 217);   // #6D28D9
    public static readonly Color CompletedBg = Color.FromArgb(220, 252, 231);   // #DCFCE7
    public static readonly Color CompletedText = Color.FromArgb(21, 128, 61);   // #15803D
    public static readonly Color CancelledBg = Color.FromArgb(254, 226, 226);   // #FEE2E2
    public static readonly Color CancelledText = Color.FromArgb(185, 28, 28);   // #B91C1C
    public static readonly Color ActiveBg = Color.FromArgb(220, 252, 231);      // #DCFCE7
    public static readonly Color ActiveText = Color.FromArgb(21, 128, 61);      // #15803D
    public static readonly Color InactiveBg = Color.FromArgb(241, 245, 249);    // #F1F5F9
    public static readonly Color InactiveText = Color.FromArgb(100, 116, 139);  // #64748B

    // Typography
    private static readonly PrivateFontCollection FontCollection = new();
    private static readonly FontFamily? CustomFontFamily;
    public static readonly string FontFamilyName;

    static AdminTheme()
    {
        try
        {
            var fontDir = ResolveFontDirectory();
            if (fontDir != null && Directory.Exists(fontDir))
            {
                var regular = Path.Combine(fontDir, "BeVietnamPro-Regular.ttf");
                var medium = Path.Combine(fontDir, "BeVietnamPro-Medium.ttf");
                var semiBold = Path.Combine(fontDir, "BeVietnamPro-SemiBold.ttf");
                var bold = Path.Combine(fontDir, "BeVietnamPro-Bold.ttf");

                if (File.Exists(regular)) FontCollection.AddFontFile(regular);
                if (File.Exists(medium)) FontCollection.AddFontFile(medium);
                if (File.Exists(semiBold)) FontCollection.AddFontFile(semiBold);
                if (File.Exists(bold)) FontCollection.AddFontFile(bold);

                CustomFontFamily = FontCollection.Families.FirstOrDefault(f =>
                    f.Name.Contains("Be Vietnam Pro", StringComparison.OrdinalIgnoreCase))
                    ?? FontCollection.Families.FirstOrDefault();
            }
        }
        catch
        {
            CustomFontFamily = null;
        }

        FontFamilyName = CustomFontFamily?.Name ?? "Segoe UI";
    }

    private static string? ResolveFontDirectory()
    {
        // 1. Check local subfolder relative to executable (when copied by build)
        var localDir = Path.Combine(AppContext.BaseDirectory, "fonts", "be-vietnam-pro");
        if (Directory.Exists(localDir)) return localDir;

        // 2. Search up directory tree for PhoneStore.Web/wwwroot/fonts/be-vietnam-pro
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 6 && current != null; i++)
        {
            var candidate = Path.Combine(current.FullName, "src", "PhoneStore.Web", "wwwroot", "fonts", "be-vietnam-pro");
            if (Directory.Exists(candidate)) return candidate;

            candidate = Path.Combine(current.FullName, "PhoneStore.Web", "wwwroot", "fonts", "be-vietnam-pro");
            if (Directory.Exists(candidate)) return candidate;

            current = current.Parent;
        }

        // 3. Check CurrentDirectory
        var cdCandidate = Path.Combine(Environment.CurrentDirectory, "src", "PhoneStore.Web", "wwwroot", "fonts", "be-vietnam-pro");
        if (Directory.Exists(cdCandidate)) return cdCandidate;

        return null;
    }

    public static Font CreateFont(float size, FontStyle style = FontStyle.Regular)
    {
        if (CustomFontFamily != null)
        {
            try
            {
                return new Font(CustomFontFamily, size, style, GraphicsUnit.Point);
            }
            catch
            {
                // Fallback to Segoe UI if style not supported
            }
        }
        return new Font("Segoe UI", size, style, GraphicsUnit.Point);
    }

    public static Label CreateLabel(string text, float size = 10, bool bold = false)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = CreateFont(size, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = Navy,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            UseMnemonic = false
        };
    }

    public static void StyleButton(Button button, bool primary = false, int minHeight = 40)
    {
        button.Font = CreateFont(9.5f, FontStyle.Regular);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = primary ? PrimaryDark : Surface;
        button.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(12, 58, 150) : Color.FromArgb(232, 238, 248);
        button.ForeColor = primary ? Color.White : Navy;
        button.BackColor = primary ? Primary : Color.White;
        button.UseVisualStyleBackColor = false;
        button.MinimumSize = new Size(0, minHeight);
        button.Padding = new Padding(14, 6, 14, 6);
        button.Cursor = Cursors.Hand;
    }

    public static Panel CreateCard(int padding = 24)
    {
        var panel = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.White,
            Padding = new Padding(padding),
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

    public static Panel CreateInputFrame(Control input, int height = 42)
    {
        var frame = new Panel
        {
            Dock = DockStyle.Fill,
            Height = height,
            Padding = new Padding(12, 10, 12, 8),
            BackColor = Color.White,
            Margin = Padding.Empty
        };

        var isFocused = false;
        frame.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(isFocused ? Primary : Border, isFocused ? 1.5f : 1f);
            using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, frame.Width - 1, frame.Height - 1), 6);
            e.Graphics.DrawPath(pen, path);
        };

        input.Enter += (_, _) => { isFocused = true; frame.Invalidate(); };
        input.Leave += (_, _) => { isFocused = false; frame.Invalidate(); };

        return frame;
    }

    public static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(rect.Location, new Size(diameter, diameter));

        if (diameter > rect.Width || diameter > rect.Height)
        {
            path.AddRectangle(rect);
            return path;
        }

        // Top left
        path.AddArc(arc, 180, 90);
        // Top right
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        // Bottom right
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        // Bottom left
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }

    public static void DrawBadge(Graphics g, Rectangle bounds, string text, Color bg, Color textColor, float fontSize = 8.5f)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(bg);
        using var path = CreateRoundedRectanglePath(bounds, 5);
        g.FillPath(brush, path);

        using var font = CreateFont(fontSize, FontStyle.Bold);
        TextRenderer.DrawText(g, text, font, bounds, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }

    public static void DrawIcon(Graphics g, string name, Rectangle bounds, Color color)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(color);

        var x = bounds.X;
        var y = bounds.Y;
        var w = bounds.Width;
        var h = bounds.Height;

        switch (name.ToLowerInvariant())
        {
            case "dashboard":
                // 4 squares
                var halfW = (w - 3) / 2;
                var halfH = (h - 3) / 2;
                g.DrawRectangle(pen, x, y, halfW, halfH);
                g.DrawRectangle(pen, x + halfW + 3, y, halfW, halfH);
                g.DrawRectangle(pen, x, y + halfH + 3, halfW, halfH);
                g.DrawRectangle(pen, x + halfW + 3, y + halfH + 3, halfW, halfH);
                break;

            case "products":
                // Smartphone outline + home bar
                var phoneW = (int)(w * 0.65f);
                var phoneX = x + (w - phoneW) / 2;
                using (var path = CreateRoundedRectanglePath(new Rectangle(phoneX, y, phoneW, h), 3))
                {
                    g.DrawPath(pen, path);
                }
                // Home indicator line
                g.DrawLine(pen, phoneX + 4, y + h - 3, phoneX + phoneW - 4, y + h - 3);
                // Top speaker dot
                g.FillEllipse(brush, phoneX + phoneW / 2 - 1, y + 2, 2, 2);
                break;

            case "brands":
                // Tag / Diamond
                var tagPts = new[]
                {
                    new Point(x + 2, y + h / 2),
                    new Point(x + w / 2, y + 2),
                    new Point(x + w - 2, y + 2),
                    new Point(x + w - 2, y + h - 2),
                    new Point(x + w / 2, y + h - 2)
                };
                g.DrawPolygon(pen, tagPts);
                g.FillEllipse(brush, x + w - 6, y + 5, 3, 3);
                break;

            case "categories":
                // Folder / Grid stack
                g.DrawLine(pen, x + 2, y + 4, x + w / 2 - 2, y + 4);
                g.DrawLine(pen, x + w / 2 - 2, y + 4, x + w / 2 + 1, y + 7);
                g.DrawLine(pen, x + w / 2 + 1, y + 7, x + w - 2, y + 7);
                using (var path = CreateRoundedRectanglePath(new Rectangle(x + 2, y + 7, w - 4, h - 9), 2))
                {
                    g.DrawPath(pen, path);
                }
                break;

            case "customers":
                // User head & shoulders
                var headRadius = (int)(w * 0.22f);
                var headCenter = x + w / 2;
                g.DrawEllipse(pen, headCenter - headRadius, y + 1, headRadius * 2, headRadius * 2);
                // Shoulders arc
                g.DrawArc(pen, x + 2, y + headRadius * 2 + 2, w - 4, (h - headRadius * 2) * 1.6f, 195, 150);
                break;

            case "orders":
                // Shopping bag / receipt
                var bagW = (int)(w * 0.75f);
                var bagX = x + (w - bagW) / 2;
                using (var path = CreateRoundedRectanglePath(new Rectangle(bagX, y + 5, bagW, h - 6), 2))
                {
                    g.DrawPath(pen, path);
                }
                // Handle
                g.DrawArc(pen, bagX + 3, y + 1, bagW - 6, 7, 180, 180);
                break;

            case "reports":
                // 3 rising bars
                var barW = (w - 6) / 3;
                g.DrawLine(pen, x + 1, y + h - 1, x + w - 1, y + h - 1); // baseline
                g.FillRectangle(brush, x + 2, y + h - (int)(h * 0.4f), barW - 1, (int)(h * 0.4f) - 1);
                g.FillRectangle(brush, x + 2 + barW + 1, y + h - (int)(h * 0.7f), barW - 1, (int)(h * 0.7f) - 1);
                g.FillRectangle(brush, x + 2 + (barW + 1) * 2, y + h - (int)(h * 0.95f), barW - 1, (int)(h * 0.95f) - 1);
                break;

            case "logout":
                // Door + arrow
                g.DrawLine(pen, x + w / 2, y + 2, x + 2, y + 2);
                g.DrawLine(pen, x + 2, y + 2, x + 2, y + h - 2);
                g.DrawLine(pen, x + 2, y + h - 2, x + w / 2, y + h - 2);
                // Exit arrow
                g.DrawLine(pen, x + 6, y + h / 2, x + w - 2, y + h / 2);
                g.DrawLine(pen, x + w - 6, y + h / 2 - 4, x + w - 2, y + h / 2);
                g.DrawLine(pen, x + w - 6, y + h / 2 + 4, x + w - 2, y + h / 2);
                break;

            case "revenue":
                // Currency coin with vertical and horizontal accent
                var coinR = (int)(w * 0.42f);
                g.DrawEllipse(pen, x + w / 2 - coinR, y + h / 2 - coinR, coinR * 2, coinR * 2);
                g.DrawLine(pen, x + w / 2 - (int)(coinR * 0.45f), y + h / 2, x + w / 2 + (int)(coinR * 0.45f), y + h / 2);
                g.DrawLine(pen, x + w / 2, y + h / 2 - (int)(coinR * 0.55f), x + w / 2, y + h / 2 + (int)(coinR * 0.55f));
                break;

            case "filter":
                // Clean funnel filter icon
                var funnelPts = new[]
                {
                    new Point(x + 1, y + 2),
                    new Point(x + w - 1, y + 2),
                    new Point(x + w / 2 + 2, y + h / 2 + 1),
                    new Point(x + w / 2 + 2, y + h - 2),
                    new Point(x + w / 2 - 2, y + h - 1),
                    new Point(x + w / 2 - 2, y + h / 2 + 1)
                };
                g.DrawPolygon(pen, funnelPts);
                break;

            case "chevron-left":
                g.DrawLines(pen, new Point[]
                {
                    new Point(x + (int)(w * 0.64f), y + 2),
                    new Point(x + (int)(w * 0.36f), y + h / 2),
                    new Point(x + (int)(w * 0.64f), y + h - 2)
                });
                break;

            case "chevron-right":
                g.DrawLines(pen, new Point[]
                {
                    new Point(x + (int)(w * 0.36f), y + 2),
                    new Point(x + (int)(w * 0.64f), y + h / 2),
                    new Point(x + (int)(w * 0.36f), y + h - 2)
                });
                break;
        }
    }
}
