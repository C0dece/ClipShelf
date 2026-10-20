using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PasteImageAsFile
{
    public static class ThemeHelper
    {
        public static event Action ThemeChanged;

        static ThemeHelper()
        {
            try
            {
                SystemEvents.UserPreferenceChanged += (s, e) =>
                {
                    if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.VisualStyle)
                    {
                        var handler = ThemeChanged;
                        if (handler != null) handler();
                    }
                };
            }
            catch {}
        }

        public static void NotifyThemeChanged()
        {
            try
            {
                var handler = ThemeChanged;
                if (handler != null) handler();
            }
            catch {}
        }

        public static bool IsDarkTheme()
        {
            string mode = Config.ThemeMode;
            if (string.Equals(mode, "Dark", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(mode, "Light", StringComparison.OrdinalIgnoreCase)) return false;

            // System default
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("AppsUseLightTheme");
                        if (val is int)
                        {
                            return ((int)val) == 0;
                        }
                    }
                }
            }
            catch {}

            return true; // Default dark
        }

        // Colors based on current theme (Native Windows 11 WinUI / Clipboard Flyout palette)
        public static Color Background
        {
            get { return IsDarkTheme() ? Color.FromArgb(31, 31, 31) : Color.FromArgb(243, 243, 243); }
        }

        public static Color HeaderBackground
        {
            get { return IsDarkTheme() ? Color.FromArgb(31, 31, 31) : Color.FromArgb(243, 243, 243); }
        }

        public static Color CardBackground
        {
            get { return IsDarkTheme() ? Color.FromArgb(45, 45, 45) : Color.FromArgb(255, 255, 255); }
        }

        public static Color CardHover
        {
            get { return IsDarkTheme() ? Color.FromArgb(54, 54, 54) : Color.FromArgb(248, 248, 248); }
        }

        public static Color CardBorder
        {
            get { return IsDarkTheme() ? Color.FromArgb(58, 58, 58) : Color.FromArgb(226, 226, 226); }
        }

        public static Color CardBorderHover
        {
            get { return IsDarkTheme() ? Color.FromArgb(120, 120, 120) : Color.FromArgb(180, 180, 180); }
        }

        public static Color TextPrimary
        {
            get { return IsDarkTheme() ? Color.FromArgb(255, 255, 255) : Color.FromArgb(26, 26, 26); }
        }

        public static Color TextSecondary
        {
            get { return IsDarkTheme() ? Color.FromArgb(166, 166, 166) : Color.FromArgb(96, 96, 96); }
        }

        public static Color TextMuted
        {
            get { return IsDarkTheme() ? Color.FromArgb(118, 118, 118) : Color.FromArgb(140, 140, 140); }
        }

        public static Color Accent
        {
            get
            {
                string custom = Config.AccentColor;
                if (!string.IsNullOrEmpty(custom))
                {
                    try
                    {
                        return ColorTranslator.FromHtml(custom);
                    }
                    catch {}
                }
                return IsDarkTheme() ? Color.FromArgb(96, 205, 255) : Color.FromArgb(0, 103, 192);
            }
        }

        public static Color AccentHover
        {
            get
            {
                Color baseCol = Accent;
                if (IsDarkTheme())
                {
                    int r = Math.Min(255, baseCol.R + 25);
                    int g = Math.Min(255, baseCol.G + 25);
                    int b = Math.Min(255, baseCol.B + 18);
                    return Color.FromArgb(r, g, b);
                }
                else
                {
                    int r = Math.Max(0, baseCol.R - 25);
                    int g = Math.Max(0, baseCol.G - 25);
                    int b = Math.Max(0, baseCol.B - 25);
                    return Color.FromArgb(r, g, b);
                }
            }
        }

        public static Color SuperHubSecondaryAccent
        {
            get
            {
                string custom = Config.SuperHubAccentColor;
                if (!string.IsNullOrEmpty(custom))
                {
                    try
                    {
                        return ColorTranslator.FromHtml(custom);
                    }
                    catch {}
                }
                return Color.FromArgb(16, 185, 129); // Изумрудный Fluent по умолчанию
            }
        }

        public static Color SuperHubSecondaryAccentHover
        {
            get
            {
                Color c = SuperHubSecondaryAccent;
                return Color.FromArgb(
                    Math.Min(255, c.R + 28),
                    Math.Min(255, c.G + 28),
                    Math.Min(255, c.B + 28));
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);
        public const uint WDA_NONE = 0x00000000;
        public const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        public static Bitmap CaptureAcrylicBackdrop(Rectangle screenBounds, Form formToHide = null)
        {
            int w = Math.Max(64, screenBounds.Width);
            int h = Math.Max(64, screenBounds.Height);
            Bitmap result = new Bitmap(w, h);
            bool isDark = IsDarkTheme();

            double prevOpacity = 1.0;
            bool wasVisible = false;

            if (formToHide != null && !formToHide.IsDisposed && formToHide.IsHandleCreated && formToHide.Visible)
            {
                try
                {
                    wasVisible = true;
                    prevOpacity = formToHide.Opacity;
                    formToHide.Opacity = 0.0;
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(20);
                }
                catch {}
            }

            try
            {
                using (Bitmap rawScreen = new Bitmap(w, h))
                {
                    bool captured = false;
                    try
                    {
                        using (Graphics gScr = Graphics.FromImage(rawScreen))
                        {
                            gScr.CopyFromScreen(screenBounds.Location, Point.Empty, new Size(w, h), CopyPixelOperation.SourceCopy);
                            captured = true;
                        }
                    }
                    catch {}
                    finally
                    {
                        if (wasVisible && formToHide != null && !formToHide.IsDisposed)
                        {
                            try
                            {
                                formToHide.Opacity = prevOpacity;
                                Application.DoEvents();
                            }
                            catch {}
                        }
                    }

                    int w1 = Math.Max(12, w / 6);
                    int h1 = Math.Max(12, h / 6);
                    int w2 = Math.Max(6, w / 18);
                    int h2 = Math.Max(6, h / 18);

                    using (Bitmap pass1 = new Bitmap(w1, h1))
                    using (Bitmap pass2 = new Bitmap(w2, h2))
                    {
                        if (captured)
                        {
                            using (Graphics g1 = Graphics.FromImage(pass1))
                            {
                                g1.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                                g1.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                g1.DrawImage(rawScreen, new Rectangle(0, 0, w1, h1));
                            }
                            using (Graphics g2 = Graphics.FromImage(pass2))
                            {
                                g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                                g2.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                g2.DrawImage(pass1, new Rectangle(0, 0, w2, h2));
                            }
                            using (Graphics g1Back = Graphics.FromImage(pass1))
                            {
                                g1Back.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                g1Back.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                g1Back.DrawImage(pass2, new Rectangle(0, 0, w1, h1));
                            }
                        }

                        using (Graphics gRes = Graphics.FromImage(result))
                        {
                            gRes.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            gRes.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                            gRes.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                            if (captured)
                            {
                                gRes.DrawImage(pass1, new Rectangle(0, 0, w, h));
                            }
                            else
                            {
                                gRes.Clear(isDark ? Color.FromArgb(24, 24, 26) : Color.FromArgb(242, 242, 245));
                            }

                            // Основная матовая тонировка Windows 11 Acrylic (Tint + Luminosity blend)
                            Color tintColor = isDark
                                ? Color.FromArgb(204, 24, 24, 26)
                                : Color.FromArgb(212, 243, 243, 246);
                            using (SolidBrush tintBrush = new SolidBrush(tintColor))
                            {
                                gRes.FillRectangle(tintBrush, 0, 0, w, h);
                            }

                            // Мягкое атмосферное свечение Acrylic (как в поиске/пуске Windows 11)
                            if (isDark)
                            {
                                using (var grad = new System.Drawing.Drawing2D.LinearGradientBrush(
                                    new Rectangle(0, 0, w, h),
                                    Color.FromArgb(14, 180, 195, 220),
                                    Color.FromArgb(18, 120, 105, 85),
                                    35f))
                                {
                                    gRes.FillRectangle(grad, 0, 0, w, h);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                using (Graphics gFallback = Graphics.FromImage(result))
                {
                    gFallback.Clear(isDark ? Color.FromArgb(28, 28, 30) : Color.FromArgb(243, 243, 243));
                }
            }

            return result;
        }

        public static void DrawAcrylicBackground(Graphics g, Control ctrl, Form rootForm, Bitmap acrylicBackdrop)
        {
            if (g == null || ctrl == null) return;

            // Всегда очищаем базовым цветом темы, исключая черные полосы и артефакты при ресайзе
            g.Clear(Background);

            if (acrylicBackdrop != null && rootForm != null && !rootForm.IsDisposed)
            {
                try
                {
                    Point backdropOrigin = Point.Empty;
                    ClipboardFlyoutForm flyout = rootForm as ClipboardFlyoutForm;
                    if (flyout != null)
                    {
                        backdropOrigin = flyout.BackdropOrigin;
                    }

                    Point pt = Point.Empty;
                    Control cur = ctrl;
                    while (cur != null && cur != rootForm)
                    {
                        pt.X += cur.Left;
                        pt.Y += cur.Top;
                        cur = cur.Parent;
                    }

                    int screenX = rootForm.Left + pt.X;
                    int screenY = rootForm.Top + pt.Y;
                    int srcX = screenX - backdropOrigin.X;
                    int srcY = screenY - backdropOrigin.Y;

                    Rectangle srcRect = new Rectangle(srcX, srcY, ctrl.Width, ctrl.Height);
                    Rectangle bmpRect = new Rectangle(0, 0, acrylicBackdrop.Width, acrylicBackdrop.Height);
                    Rectangle safeSrc = Rectangle.Intersect(srcRect, bmpRect);
                    if (safeSrc.Width > 0 && safeSrc.Height > 0)
                    {
                        Rectangle safeDest = new Rectangle(safeSrc.X - srcX, safeSrc.Y - srcY, safeSrc.Width, safeSrc.Height);
                        g.DrawImage(acrylicBackdrop, safeDest, safeSrc, GraphicsUnit.Pixel);
                    }
                }
                catch {}
            }
        }

        public static Color AccentBackground
        {
            get
            {
                if (IsDarkTheme())
                {
                    return Color.FromArgb(58, 58, 58);
                }
                else
                {
                    Color baseCol = Accent;
                    int r = Math.Min(255, baseCol.R + (255 - baseCol.R) * 4 / 5);
                    int g = Math.Min(255, baseCol.G + (255 - baseCol.G) * 4 / 5);
                    int b = Math.Min(255, baseCol.B + (255 - baseCol.B) * 4 / 5);
                    return Color.FromArgb(r, g, b);
                }
            }
        }

        public static Color ScrollBarTrack
        {
            get { return IsDarkTheme() ? Color.FromArgb(31, 31, 31) : Color.FromArgb(243, 243, 243); }
        }

        public static Color ScrollBarThumb
        {
            get { return IsDarkTheme() ? Color.FromArgb(158, 158, 158) : Color.FromArgb(170, 170, 170); }
        }

        public static Color ScrollBarThumbHover
        {
            get { return IsDarkTheme() ? Color.FromArgb(195, 195, 195) : Color.FromArgb(140, 140, 140); }
        }

        public static Color ScrollBarThumbActive
        {
            get { return IsDarkTheme() ? Color.FromArgb(225, 225, 225) : Color.FromArgb(110, 110, 110); }
        }

        public static Color BadgeBackground
        {
            get { return IsDarkTheme() ? Color.FromArgb(52, 52, 52) : Color.FromArgb(232, 232, 232); }
        }

        public static Color BadgeText
        {
            get { return IsDarkTheme() ? Color.FromArgb(220, 220, 220) : Color.FromArgb(60, 60, 60); }
        }

        public static Color Separator
        {
            get { return IsDarkTheme() ? Color.FromArgb(55, 55, 55) : Color.FromArgb(224, 224, 224); }
        }

        public static Color ButtonHover
        {
            get { return IsDarkTheme() ? Color.FromArgb(52, 52, 52) : Color.FromArgb(232, 232, 232); }
        }

        public static Color ButtonActive
        {
            get { return IsDarkTheme() ? Color.FromArgb(64, 64, 64) : Color.FromArgb(218, 218, 218); }
        }

        public static Color ButtonPressed
        {
            get { return ButtonActive; }
        }

        public static void ApplyButtonStyle(Button btn, bool isActive = false)
        {
            if (btn == null) return;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ButtonHover;
            btn.FlatAppearance.MouseDownBackColor = ButtonPressed;
            if (isActive)
            {
                btn.BackColor = AccentBackground;
                btn.ForeColor = Accent;
            }
        }

        public static ToolTip CreateFluentToolTip()
        {
            var tt = new ToolTip();
            tt.AutoPopDelay = 6000;
            tt.InitialDelay = 300;
            tt.ReshowDelay = 150;
            tt.OwnerDraw = true;

            tt.Draw += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                bool isDark = IsDarkTheme();
                Color bg = isDark ? Color.FromArgb(43, 43, 43) : Color.FromArgb(250, 250, 250);
                Color border = isDark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(205, 205, 205);
                Color textCol = isDark ? Color.FromArgb(240, 240, 240) : Color.FromArgb(25, 25, 25);

                Rectangle rect = new Rectangle(0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
                using (var b = new SolidBrush(bg))
                {
                    g.FillRectangle(b, e.Bounds);
                }
                using (var p = new Pen(border, 1))
                {
                    g.DrawRectangle(p, rect);
                }

                TextRenderer.DrawText(g, e.ToolTipText, new Font("Segoe UI", 9f),
                    new Rectangle(8, 4, e.Bounds.Width - 16, e.Bounds.Height - 8),
                    textCol, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };

            tt.Popup += (s, e) => {
                string text = tt.GetToolTip(e.AssociatedControl);
                Size sz = TextRenderer.MeasureText(text, new Font("Segoe UI", 9f));
                e.ToolTipSize = new Size(sz.Width + 18, Math.Max(24, sz.Height + 10));
            };

            return tt;
        }
    }
}
