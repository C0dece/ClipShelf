using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace PasteImageAsFile
{
    public class ClipboardFlyoutForm : Form
    {
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        const int DWMWCP_ROUND = 2;
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        const byte VK_CONTROL = 0x11;
        const byte VK_V = 0x56;
        const uint KEYEVENTF_KEYUP = 0x0002;

        private const int WM_NCHITTEST = 0x0084;
        private const int WM_GETMINMAXINFO = 0x0024;
        private const int WM_SIZING = 0x0214;
        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public Point ptReserved;
            public Point ptMaxSize;
            public Point ptMaxPosition;
            public Point ptMinTrackSize;
            public Point ptMaxTrackSize;
        }

        private class FlyoutViewportPanel : Panel
        {
            private const int WM_NCHITTEST = 0x0084;
            private const int HTTRANSPARENT = -1;

            public FlyoutViewportPanel()
            {
                this.DoubleBuffered = true;
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                ClipboardFlyoutForm f = this.FindForm() as ClipboardFlyoutForm;
                if (f != null && f.acrylicBackdrop != null)
                {
                    ThemeHelper.DrawAcrylicBackground(e.Graphics, this, f, f.acrylicBackdrop);
                    return;
                }
                e.Graphics.Clear(ThemeHelper.Background);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_NCHITTEST)
                {
                    Point pt = this.PointToClient(new Point(m.LParam.ToInt32()));
                    int border = 8;
                    if (pt.X >= this.Width - border || pt.Y >= this.Height - border || pt.X <= border)
                    {
                        m.Result = (IntPtr)HTTRANSPARENT;
                        return;
                    }
                }
                base.WndProc(ref m);
            }
        }

        private class FlyoutTopBarPanel : Panel
        {
            private const int WM_NCHITTEST = 0x0084;
            private const int HTTRANSPARENT = -1;

            public FlyoutTopBarPanel()
            {
                this.DoubleBuffered = true;
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                ClipboardFlyoutForm f = this.FindForm() as ClipboardFlyoutForm;
                if (f != null && f.acrylicBackdrop != null)
                {
                    ThemeHelper.DrawAcrylicBackground(e.Graphics, this, f, f.acrylicBackdrop);
                    return;
                }
                e.Graphics.Clear(ThemeHelper.Background);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_NCHITTEST)
                {
                    Point pt = this.PointToClient(new Point(m.LParam.ToInt32()));
                    int border = 8;
                    if (pt.Y <= border || pt.X <= border || pt.X >= this.Width - border)
                    {
                        m.Result = (IntPtr)HTTRANSPARENT;
                        return;
                    }
                }
                base.WndProc(ref m);
            }
        }

        private class AcrylicPanel : Panel
        {
            public AcrylicPanel()
            {
                this.DoubleBuffered = true;
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                ClipboardFlyoutForm f = this.FindForm() as ClipboardFlyoutForm;
                if (f != null && f.acrylicBackdrop != null)
                {
                    ThemeHelper.DrawAcrylicBackground(e.Graphics, this, f, f.acrylicBackdrop);
                    return;
                }
                e.Graphics.Clear(ThemeHelper.Background);
            }
        }

        private class AcrylicFlowPanel : FlowLayoutPanel
        {
            public AcrylicFlowPanel()
            {
                this.DoubleBuffered = true;
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                ClipboardFlyoutForm f = this.FindForm() as ClipboardFlyoutForm;
                if (f != null && f.acrylicBackdrop != null)
                {
                    ThemeHelper.DrawAcrylicBackground(e.Graphics, this, f, f.acrylicBackdrop);
                    return;
                }
                e.Graphics.Clear(ThemeHelper.Background);
            }
        }

        private class AcrylicCardPanel : Panel
        {
            public bool IsCardHovered = false;

            public AcrylicCardPanel()
            {
                this.DoubleBuffered = true;
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                ClipboardFlyoutForm f = this.FindForm() as ClipboardFlyoutForm;
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                if (f != null && f.IsCornerDockMode)
                {
                    // В угловом секторе форма имеет фон ThemeHelper.Background.
                    // Очищаем область карточки фоном формы, гарантируя полное стирание мусора
                    g.Clear(ThemeHelper.Background);

                    Rectangle rCard = new Rectangle(0, 0, Math.Max(1, this.Width - 1), Math.Max(1, this.Height - 1));
                    using (var path = CreateRoundedPath(rCard, 7))
                    {
                        Color cardBg = ThemeHelper.IsDarkTheme()
                            ? (IsCardHovered ? Color.FromArgb(46, 46, 46) : Color.FromArgb(34, 34, 34))
                            : (IsCardHovered ? Color.FromArgb(255, 255, 255) : Color.FromArgb(244, 244, 244));
                        using (var brush = new SolidBrush(cardBg))
                        {
                            g.FillPath(brush, path);
                        }
                    }
                    return;
                }

                if (f != null && f.acrylicBackdrop != null)
                {
                    ThemeHelper.DrawAcrylicBackground(g, this, f, f.acrylicBackdrop);
                }
                else
                {
                    g.Clear(ThemeHelper.Background);
                }

                Color glassFill = ThemeHelper.IsDarkTheme()
                    ? Color.FromArgb(IsCardHovered ? 36 : 22, 255, 255, 255)
                    : Color.FromArgb(IsCardHovered ? 242 : 218, 255, 255, 255);
                using (var path = CreateRoundedPath(new Rectangle(0, 0, Math.Max(1, this.Width - 1), Math.Max(1, this.Height - 1)), 6))
                using (var brush = new SolidBrush(glassFill))
                {
                    g.FillPath(brush, path);
                }
            }
        }

        private class CornerEmptyCardPanel : Panel
        {
            public bool IsHubMode = true;
            public string MainTitle = "Полка SuperHub пуста";
            public string SubTitle = "Перетащите сюда файлы или нажмите Ctrl+Alt+C";

            public CornerEmptyCardPanel()
            {
                this.DoubleBuffered = true;
                this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                this.BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle r = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
                using (var path = CreateRoundedPath(r, 12))
                using (var br = new SolidBrush(Color.FromArgb(38, 255, 255, 255)))
                using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255), 1.2f))
                {
                    g.FillPath(br, path);
                    g.DrawPath(pen, path);
                }

                int cx = this.Width / 2;
                Rectangle iconRect = new Rectangle(cx - 16, 12, 32, 32);
                Color iconColor = IsHubMode ? ThemeHelper.SuperHubSecondaryAccent : ThemeHelper.Accent;
                DrawCategoryFlatIcon(g, iconRect, 0, iconColor, IsHubMode);

                using (var fTitle = new Font("Segoe UI Semibold", 10.5f))
                using (var bTitle = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(MainTitle, fTitle, bTitle, new RectangleF(6, 48, this.Width - 12, 22), sf);
                }

                using (var fSub = new Font("Segoe UI", 8.5f))
                using (var bSub = new SolidBrush(Color.FromArgb(190, 255, 255, 255)))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(SubTitle, fSub, bSub, new RectangleF(6, 70, this.Width - 12, 32), sf);
                }
            }
        }

        private static bool IsOurAppWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            return pid == (uint)Process.GetCurrentProcess().Id;
        }

        private static ClipboardFlyoutForm currentInstance;
        public static ClipboardFlyoutForm CurrentInstance { get { return currentInstance; } }
        private static readonly object instanceLock = new object();

        public int CurrentMainTab { get { return currentMainTab; } }

        public static bool IsFlyoutOpen
        {
            get { return currentInstance != null && !currentInstance.IsDisposed && currentInstance.Visible; }
        }

        public static void CloseActiveFlyout()
        {
            lock (instanceLock)
            {
                if (currentInstance != null && !currentInstance.IsDisposed)
                {
                    currentInstance.CloseFlyout();
                }
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                cp.ExStyle |= 0x02000000;    // WS_EX_COMPOSITED (Композитный DWM буфер - устраняет мерцание всех дочерних окон)
                return cp;
            }
        }

        private Bitmap acrylicBackdrop;
        private Point backdropOrigin = Point.Empty;

        public Point BackdropOrigin
        {
            get { return backdropOrigin; }
        }

        public void UpdateAcrylicBackdrop(Point screenLoc)
        {
            try
            {
                Screen scr = Screen.FromPoint(screenLoc);
                if (scr == null) scr = Screen.PrimaryScreen;
                Rectangle captureRect = (scr != null) ? scr.Bounds : new Rectangle(screenLoc, this.Size);

                Bitmap old = acrylicBackdrop;
                backdropOrigin = captureRect.Location;
                acrylicBackdrop = ThemeHelper.CaptureAcrylicBackdrop(captureRect, this);
                this.BackgroundImage = null;
                if (old != null) old.Dispose();
            }
            catch {}
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (acrylicBackdrop != null)
            {
                ThemeHelper.DrawAcrylicBackground(e.Graphics, this, this, acrylicBackdrop);
                return;
            }
            e.Graphics.Clear(ThemeHelper.Background);
        }

        private IntPtr previousForegroundWindow = IntPtr.Zero;
        private FlyoutTopBarPanel pnlTopBar;
        private Label lblAppHeader;
        private Label lblSectionTitle;
        private Button btnOpenSettings;
        private Panel pnlMainTabs;
        private Button btnMainClipboard;
        private Button btnMainSuperHub;
        private int currentMainTab = Math.Max(0, Math.Min(1, Config.LastActiveFlyoutTab)); // 0 = Буфер обмена, 1 = SuperHub

        private Panel pnlSubBar;
        private Panel pnlSubTabsClip;
        private FlowLayoutPanel flpSubTabs;
        private int currentFilterIndex = 0; // 0 = Все, 1 = Текст, 2 = Снимки, 3 = Ссылки, 4 = Папки, 5 = Файлы, 6 = Закрепленные
        private readonly string[] filterNames = new string[] {
            "Переключить режим: Буфер обмена / SuperHub",
            "Все категории",
            "Текст",
            "Снимки",
            "Ссылки",
            "Папки",
            "Файлы",
            "Закрепленные"
        };
        private readonly List<Button> filterButtons = new List<Button>();
        private int subTabsScrollX = 0;
        private int foldersScrollX = 0;
        private Button btnSubTabsScrollLeft;
        private Button btnSubTabsScrollRight;

        private Button btnDropHint;
        private Button btnClickAction;
        private Panel pnlFolders;
        private FlowLayoutPanel flpFolders;
        private Button btnAddFolder;
        private readonly List<Button> folderButtons = new List<Button>();

        private static readonly Font CardTitleFont = new Font("Segoe UI Semibold", 9.5f);
        private static readonly Font CardSubFont = new Font("Segoe UI", 8.25f);
        private System.Windows.Forms.Timer searchDebounceTimer;
        private bool superHubNeedsRefresh = true;
        private bool clipboardNeedsRefresh = true;

        private Panel pnlSearch;
        private Panel searchBoxBg;
        private TextBox txtSearch;
        private FlyoutViewportPanel pnlViewport;
        private Panel cardsContainer;
        private Panel cardsContainerClipboard;
        private Panel cardsContainerSuperHub;
        private Button btnPinWindow;
        private Button btnClose;
        private Button btnMinimize;
        private Button btnClearAll;
        private Button btnDragMode;
        private Button btnAddNote;
        private Button btnSearchToggle;
        private Button btnDomePrev;
        private Button btnDomeNext;
        private CornerEmptyCardPanel pnlCornerEmpty;
        private bool isSearchOpen = false;
        private ToolTip toolTip;
        private bool isWindowPinned = false;
        private bool isShowingModalDialog = false;
        private bool isConfirmingClear = false;
        private System.Windows.Forms.Timer clearConfirmTimer;
        private int lastLayoutCardWidth = -1;

        // Позиционирование и ресайз относительно маркера
        private bool isDockFlyout = false;
        private string dockPositionMode = "";
        public bool IsHorizontalDockMode
        {
            get
            {
                return isDockFlyout && !string.IsNullOrEmpty(dockPositionMode) &&
                       !IsCornerDockMode &&
                       (string.Equals(dockPositionMode, "BottomCenter", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(dockPositionMode, "TopCenter", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(dockPositionMode, "Bottom", StringComparison.OrdinalIgnoreCase));
            }
        }

        public bool IsCornerDockMode
        {
            get
            {
                // Отключено: для угловых позиций используется стандартное вертикальное меню в стиле бокового дока
                return false;
            }
        }

        public static GraphicsPath CreateCornerPiePath(int w, int h, string pos)
        {
            var path = new GraphicsPath();
            if (string.Equals(pos, "CornerBottomLeft", StringComparison.OrdinalIgnoreCase))
            {
                path.AddPie(-w, 0, w * 2, h * 2, 270, 90);
            }
            else if (string.Equals(pos, "CornerTopRight", StringComparison.OrdinalIgnoreCase))
            {
                path.AddPie(0, -h, w * 2, h * 2, 90, 90);
            }
            else if (string.Equals(pos, "CornerTopLeft", StringComparison.OrdinalIgnoreCase))
            {
                path.AddPie(-w, -h, w * 2, h * 2, 0, 90);
            }
            else // CornerBottomRight default
            {
                path.AddPie(0, 0, w * 2, h * 2, 180, 90);
            }
            return path;
        }

        public static void GetCornerGeometry(int w, int h, string pos, out PointF origin, out float startAngle, out float sweepAngle, out float bisectorAngle)
        {
            sweepAngle = 90f;
            if (string.Equals(pos, "CornerBottomLeft", StringComparison.OrdinalIgnoreCase))
            {
                origin = new PointF(0, h);
                startAngle = 270f;
                bisectorAngle = 315f;
            }
            else if (string.Equals(pos, "CornerTopRight", StringComparison.OrdinalIgnoreCase))
            {
                origin = new PointF(w, 0);
                startAngle = 90f;
                bisectorAngle = 135f;
            }
            else if (string.Equals(pos, "CornerTopLeft", StringComparison.OrdinalIgnoreCase))
            {
                origin = new PointF(0, 0);
                startAngle = 0f;
                bisectorAngle = 45f;
            }
            else // CornerBottomRight default
            {
                origin = new PointF(w, h);
                startAngle = 180f;
                bisectorAngle = 225f;
            }
        }

        public static PointF GetPolarPoint(PointF origin, float radius, float angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            return new PointF((float)(origin.X + radius * Math.Cos(rad)), (float)(origin.Y + radius * Math.Sin(rad)));
        }

        public static void DrawCornerArc(Graphics g, Pen pen, int w, int h, string pos, float r)
        {
            if (string.Equals(pos, "CornerBottomLeft", StringComparison.OrdinalIgnoreCase))
            {
                g.DrawArc(pen, -r, h - r, r * 2f, r * 2f, 270, 90);
            }
            else if (string.Equals(pos, "CornerTopRight", StringComparison.OrdinalIgnoreCase))
            {
                g.DrawArc(pen, w - r, -r, r * 2f, r * 2f, 90, 90);
            }
            else if (string.Equals(pos, "CornerTopLeft", StringComparison.OrdinalIgnoreCase))
            {
                g.DrawArc(pen, -r, -r, r * 2f, r * 2f, 0, 90);
            }
            else // CornerBottomRight default
            {
                g.DrawArc(pen, w - r, h - r, r * 2f, r * 2f, 180, 90);
            }
        }

        private int arcScrollIndex = 0;

        public void UpdateCornerRegion()
        {
            if (!IsCornerDockMode)
            {
                this.Region = null;
                return;
            }

            int w = this.Width;
            int h = this.Height;
            if (w <= 20 || h <= 20) return;

            try
            {
                using (var path = CreateCornerPiePath(w, h, dockPositionMode))
                {
                    this.Region = new Region(path);
                }
            }
            catch {}
        }
        private int anchorMarkerCenterY = 0;
        private Screen currentDockScreen = null;
        private Rectangle dockMarkerBounds = Rectangle.Empty;

        // Таймер автоскрытия при уходе мыши
        private System.Windows.Forms.Timer mouseLeaveTimer;
        private int mouseAwayCounter = 0;
        // Кастомный Fluent скроллбар (вертикальный и горизонтальный)
        private int scrollY = 0;
        private int maxScrollY = 0;
        private int scrollX = 0;
        private int maxScrollX = 0;
        private int dragStartMouseX = 0;
        private int dragStartScrollX = 0;
        private bool isDraggingScroll = false;
        public static bool IsLeftMouseButtonDown()
        {
            try
            {
                return (GetAsyncKeyState(0x01) & 0x8000) != 0;
            }
            catch
            {
                return (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left;
            }
        }

        public static bool IsGlobalDraggingActive { get; private set; }
        public static long GlobalDragEndTimeTicks { get; private set; }
        public static string GlobalDraggingItemId { get; private set; }
        public static readonly HashSet<string> GlobalDraggingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static bool IsOurInternalDrag(IDataObject data)
        {
            if (IsGlobalDraggingActive) return true;

            long now = DateTime.UtcNow.Ticks;
            if (now - GlobalDragEndTimeTicks < TimeSpan.FromMilliseconds(1500).Ticks)
            {
                if (data != null)
                {
                    try
                    {
                        if (data.GetDataPresent(DataFormats.FileDrop))
                        {
                            string[] files = data.GetData(DataFormats.FileDrop) as string[];
                            if (files != null && files.Length > 0)
                            {
                                foreach (var f in files)
                                {
                                    if (GlobalDraggingPaths.Contains(f)) return true;
                                }
                            }
                        }
                    }
                    catch {}
                }
                return true;
            }

            if (data != null)
            {
                try
                {
                    if (data.GetDataPresent("PasteImageAsFile_InternalDrag")) return true;
                    if (data.GetDataPresent("PasteImageAsFile_SourceItemId")) return true;
                }
                catch {}
            }

            return false;
        }

        private static bool isGeneratingScreenshots = false;

        private bool isDraggingItemOut = false;
        private int dragStartMouseY = 0;
        private int dragStartScrollY = 0;
        private bool isHoveringScroll = false;

        private Point openTargetLocation;
        private Point openStartPoint;

        public ClipboardFlyoutForm(IntPtr prevFg, bool isDock = false, string dockPosition = null, Screen dockScreen = null)
        {
            Logger.Log("ClipboardFlyoutForm constructor start");
            this.previousForegroundWindow = prevFg;
            this.isDockFlyout = isDock;
            this.dockPositionMode = dockPosition ?? "";
            this.currentDockScreen = dockScreen;

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;

            if (this.IsCornerDockMode)
            {
                int rSize = 450;
                this.Size = new Size(rSize, rSize);
                this.MinimumSize = new Size(380, 380);
            }
            else if (this.IsHorizontalDockMode)
            {
                int initW = Math.Max(480, Config.SuperHubWidthHorizontal);
                int initH = Math.Max(160, Config.SuperHubHeightHorizontal);
                this.Size = new Size(initW, initH);
                this.MinimumSize = new Size(480, 160);
            }
            else
            {
                int initW = Math.Max(310, Config.ClipboardFlyoutWidth);
                int initH = Math.Max(420, Config.ClipboardFlyoutHeight);
                this.Size = new Size(initW, initH);
                this.MinimumSize = new Size(310, 420);
            }

            this.DoubleBuffered = true;
            this.TopMost = true;
            this.Icon = Program.GetAppIcon(32);
            this.KeyPreview = true;
            this.AllowDrop = true;
            this.Opacity = 0.96;

            toolTip = ThemeHelper.CreateFluentToolTip();
            toolTip.ShowAlways = true;
            Logger.Log("Constructor: after ToolTip");

            this.Shown += (s, e) => {
                try {
                    Logger.Log("Shown event start");
                    ApplyWindowStyles();
                    LayoutControls();
                    UpdateSubBarLayout();
                    // Предзагрузка неактивной вкладки после показа окна
                    this.BeginInvoke((Action)PreloadInactiveTab);
                    Logger.Log("Shown event end");
                } catch (Exception exShown) {
                    Logger.Log("Shown EXCEPTION: " + exShown.ToString());
                }
            };
            Logger.Log("Checkpoint 1: after Shown");

            ThemeHelper.ThemeChanged += () => {
                if (!this.IsDisposed && this.IsHandleCreated)
                {
                    this.BeginInvoke((Action)(() => {
                        ApplyTheme();
                        RefreshItems();
                    }));
                }
            };
            Logger.Log("Checkpoint 2: after ThemeChanged");

            ClipboardHistoryManager.Instance.HistoryChanged += () => {
                if (!this.IsDisposed && this.IsHandleCreated)
                {
                    this.BeginInvoke((Action)RefreshItems);
                }
            };
            Logger.Log("Checkpoint 3: after HistoryChanged");

            BuildUI();
            Logger.Log("Constructor: after BuildUI");

            ApplyTheme();
            Logger.Log("Constructor: after ApplyTheme");

            this.Deactivate += (s, e) => {
                // Если окно открыто по горячей клавише (Win+V), НЕ закрываем его при наборе текста в другом окне!
                if (!isDockFlyout) return;
                if (isWindowPinned || isShowingModalDialog || isDraggingItemOut || IsGlobalDraggingActive || IsLeftMouseButtonDown()) return;

                Rectangle b = this.Bounds;
                b.Inflate(8, 8);
                if (b.Contains(Cursor.Position)) return;

                CloseFlyout();
            };

            this.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Escape)
                {
                    CloseFlyout();
                }
                else if (e.Control && e.KeyCode == Keys.N && currentMainTab == 1)
                {
                    OpenNewNoteDialog();
                    e.SuppressKeyPress = true;
                }
            };

            // Сквозной Drag and drop для всего окна
            RegisterSuperHubDropTarget(this);
            Logger.Log("Constructor: after RegisterSuperHubDropTarget");

            // Таймер мягкого автоскрытия при уходе курсора за пределы боковой полки
            mouseLeaveTimer = new System.Windows.Forms.Timer();
            mouseLeaveTimer.Interval = 50;
            mouseLeaveTimer.Tick += OnMouseLeaveCheckTick;
            mouseLeaveTimer.Start();

            RefreshItems();
            Logger.Log("Constructor: after RefreshItems");
            Logger.Log("ClipboardFlyoutForm constructor end");
        }

        private void OnMouseLeaveCheckTick(object sender, EventArgs e)
        {
            if (isWindowPinned || isShowingModalDialog || this.IsDisposed || !this.Visible) return;
            if (isDraggingScroll || isDraggingItemOut || IsGlobalDraggingActive) return;

            // Если окно вызвано через Win+V / горячую клавишу (!isDockFlyout), оно НЕ должно закрываться от увода мыши!
            // Оно закрывается только после выбора элемента для вставки или по крестику / Esc / повторному Win+V.
            if (!isDockFlyout) return;

            // Если активно всплывающее контекстное меню Windows (класс #32768), не скрываем меню
            try
            {
                if (FindWindow("#32768", null) != IntPtr.Zero) return;
            }
            catch {}

            Point cur = Cursor.Position;
            bool isOverMenuOrMarker = false;

            Rectangle flyoutArea = this.Bounds;
            flyoutArea.Inflate(8, 8); // Умеренный буфер 8px для естественного перемещения курсора

            if (flyoutArea.Contains(cur))
            {
                isOverMenuOrMarker = true;
            }
            else
            {
                Rectangle markerArea = dockMarkerBounds;
                if (markerArea == Rectangle.Empty)
                {
                    var dock = SuperHubDockForm.Instance;
                    if (dock != null && !dock.IsDisposed && dock.Visible) markerArea = dock.Bounds;
                }
                if (markerArea != Rectangle.Empty)
                {
                    markerArea.Inflate(6, 6);
                    if (markerArea.Contains(cur)) isOverMenuOrMarker = true;
                }
            }

            if (isOverMenuOrMarker)
            {
                mouseAwayCounter = 0;
            }
            else
            {
                // Если пользователь кликнул ЛКМ вне пределов меню (по другому окну/рабочему столу),
                // закрываем меню немедленно!
                if (IsLeftMouseButtonDown())
                {
                    mouseAwayCounter = 0;
                    CloseFlyout();
                    return;
                }

                int autoCloseDelay = Config.SuperHubAutoHideDelay;
                if (autoCloseDelay > 0)
                {
                    mouseAwayCounter++;
                    int interval = mouseLeaveTimer != null ? mouseLeaveTimer.Interval : 50;
                    int requiredTicks = Math.Max(1, autoCloseDelay / interval);
                    if (mouseAwayCounter >= requiredTicks)
                    {
                        mouseAwayCounter = 0;
                        CloseFlyout();
                    }
                }
            }
        }

        private void ApplyWindowStyles()
        {
            try
            {
                int corner = IsCornerDockMode ? 1 : DWMWCP_ROUND;
                DwmSetWindowAttribute(this.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
                int dark = ThemeHelper.IsDarkTheme() ? 1 : 0;
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
                int backdrop = IsCornerDockMode ? 0 : 3; // 0 = Отключить системный DWM-акрил для углового сектора 90°, исключая паразитные прямоугольные уши
                DwmSetWindowAttribute(this.Handle, 38, ref backdrop, sizeof(int));
            }
            catch {}
        }

        private void ApplyTheme()
        {
            ApplyWindowStyles();
            this.BackColor = ThemeHelper.Background;
            this.ForeColor = ThemeHelper.TextPrimary;

            if (pnlTopBar != null) pnlTopBar.BackColor = Color.Transparent;
            if (lblAppHeader != null) lblAppHeader.ForeColor = ThemeHelper.TextSecondary;
            if (btnClose != null) btnClose.ForeColor = ThemeHelper.TextSecondary;
            if (btnPinWindow != null && !isWindowPinned) btnPinWindow.ForeColor = ThemeHelper.TextSecondary;

            if (pnlMainTabs != null) pnlMainTabs.BackColor = Color.Transparent;
            if (pnlSubBar != null) pnlSubBar.BackColor = Color.Transparent;

            if (btnClearAll != null)
            {
                btnClearAll.BackColor = Color.Transparent;
                btnClearAll.ForeColor = ThemeHelper.TextPrimary;
            }

            if (pnlSearch != null) pnlSearch.BackColor = ThemeHelper.Background;
            if (searchBoxBg != null) searchBoxBg.BackColor = ThemeHelper.CardBackground;
            if (txtSearch != null)
            {
                txtSearch.BackColor = ThemeHelper.CardBackground;
                txtSearch.ForeColor = ThemeHelper.TextPrimary;
            }

            if (btnAddNote != null)
            {
                btnAddNote.BackColor = Color.Transparent;
                btnAddNote.ForeColor = ThemeHelper.TextPrimary;
            }

            if (btnDropHint != null) btnDropHint.Invalidate();
            if (pnlViewport != null) pnlViewport.BackColor = ThemeHelper.Background;
            if (pnlFolders != null) pnlFolders.BackColor = ThemeHelper.Background;
            if (flpFolders != null) flpFolders.BackColor = ThemeHelper.Background;

            UpdateTabsVisual();
            UpdateSubBarLayout();
            this.Invalidate();
        }

        public void CloseFlyout()
        {
            if (isGeneratingScreenshots) return;
            try
            {
                if (mouseLeaveTimer != null)
                {
                    mouseLeaveTimer.Stop();
                    mouseLeaveTimer.Dispose();
                    mouseLeaveTimer = null;
                }
                try
                {
                    if (this.WindowState == FormWindowState.Normal && this.Width >= 260 && this.Height >= 160)
                    {
                        if (IsHorizontalDockMode)
                        {
                            Config.SuperHubWidthHorizontal = this.Width;
                            Config.SuperHubHeightHorizontal = this.Height;
                        }
                        else
                        {
                            Config.ClipboardFlyoutWidth = this.Width;
                            Config.ClipboardFlyoutHeight = this.Height;
                        }
                    }
                }
                catch {}
                if (acrylicBackdrop != null)
                {
                    this.BackgroundImage = null;
                    acrylicBackdrop.Dispose();
                    acrylicBackdrop = null;
                }
                this.Close();
                this.Dispose();
            }
            catch {}
            finally
            {
                lock (instanceLock)
                {
                    if (currentInstance == this) currentInstance = null;
                }
            }
        }

        public void AnimateIn(Point targetPoint, Point startPoint)
        {
            openTargetLocation = targetPoint;
            openStartPoint = targetPoint;

            // Захватываем размытый матовый фон экрана под целевой позицией ДО появления окна
            UpdateAcrylicBackdrop(targetPoint);

            this.Location = targetPoint;
            this.Opacity = 0.96;
            this.Show();
            this.BringToFront();
            // Не вызываем SetForegroundWindow(this.Handle) / this.Activate(),
            // чтобы пользователь мог продолжать печатать в своем активном окне!
            UpdateSubBarLayout();
            this.Invalidate(true);
        }

        public void AnimateIn(Point targetPoint, bool isFromBottom)
        {
            AnimateIn(targetPoint, targetPoint);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEACTIVATE = 0x0021;
            const int MA_NOACTIVATE = 3;
            if (m.Msg == WM_MOUSEACTIVATE)
            {
                // Не перехватываем фокус ввода у целевого окна при клике по карточкам или кнопкам!
                Point cur = this.PointToClient(Cursor.Position);
                if (pnlSearch != null && pnlSearch.Visible && pnlSearch.Bounds.Contains(cur))
                {
                    base.WndProc(ref m);
                    return;
                }
                m.Result = (IntPtr)MA_NOACTIVATE;
                return;
            }
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                Point screenPt = new Point(m.LParam.ToInt32());
                Point clientPt = this.PointToClient(screenPt);
                int border = 8;

                bool onLeft = clientPt.X <= border;
                bool onRight = clientPt.X >= this.ClientSize.Width - border;
                bool onTop = clientPt.Y <= border;
                bool onBottom = clientPt.Y >= this.ClientSize.Height - border;

                if (onTop && onLeft) { m.Result = (IntPtr)HTTOPLEFT; return; }
                if (onTop && onRight) { m.Result = (IntPtr)HTTOPRIGHT; return; }
                if (onBottom && onLeft) { m.Result = (IntPtr)HTBOTTOMLEFT; return; }
                if (onBottom && onRight) { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
                if (onLeft) { m.Result = (IntPtr)HTLEFT; return; }
                if (onRight) { m.Result = (IntPtr)HTRIGHT; return; }
                if (onTop) { m.Result = (IntPtr)HTTOP; return; }
                if (onBottom) { m.Result = (IntPtr)HTBOTTOM; return; }
                return;
            }
            const int WM_EXITSIZEMOVE = 0x0232;
            if (m.Msg == WM_EXITSIZEMOVE)
            {
                if (this.WindowState == FormWindowState.Normal)
                {
                    if (IsHorizontalDockMode)
                    {
                        Config.SuperHubWidthHorizontal = this.Width;
                        Config.SuperHubHeightHorizontal = this.Height;
                    }
                    else if (this.Width >= 310 && this.Height >= 420)
                    {
                        Config.ClipboardFlyoutWidth = this.Width;
                        Config.ClipboardFlyoutHeight = this.Height;
                    }

                    // Если окно переместилось на другой монитор или за пределы захваченного экрана, обновляем фон
                    Screen currentScr = Screen.FromControl(this);
                    if (currentScr != null && (backdropOrigin != currentScr.Bounds.Location || acrylicBackdrop == null || acrylicBackdrop.Width < currentScr.Bounds.Width))
                    {
                        UpdateAcrylicBackdrop(this.Location);
                    }

                    this.Invalidate(true);
                }
            }
            if (m.Msg == WM_SIZING && isDockFlyout)
            {
                int edge = m.WParam.ToInt32();
                // 3 = WMSZ_TOP, 6 = WMSZ_BOTTOM, 4 = WMSZ_TOPLEFT, 5 = WMSZ_TOPRIGHT, 7 = WMSZ_BOTTOMLEFT, 8 = WMSZ_BOTTOMRIGHT
                if (edge == 3 || edge == 6 || edge == 4 || edge == 5 || edge == 7 || edge == 8)
                {
                    RECT r = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
                    Screen s = currentDockScreen ?? Screen.FromPoint(new Point(r.Left, anchorMarkerCenterY > 0 ? anchorMarkerCenterY : r.Top));
                    Rectangle wk = s.WorkingArea;

                    if (IsHorizontalDockMode)
                    {
                        int newH = r.Bottom - r.Top;
                        if (newH < 160) newH = 160;
                        if (newH > 500) newH = 500;
                        r.Bottom = wk.Bottom - 4;
                        r.Top = r.Bottom - newH;
                        Marshal.StructureToPtr(r, m.LParam, true);
                        m.Result = (IntPtr)1;
                        return;
                    }

                    int newHVert = r.Bottom - r.Top;
                    if (newHVert < 420) newHVert = 420;
                    if (newHVert > 1400) newHVert = 1400;

                    if (dockPositionMode.IndexOf("CornerBottom", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        r.Bottom = wk.Bottom - 4;
                        r.Top = r.Bottom - newHVert;
                    }
                    else if (dockPositionMode.IndexOf("CornerTop", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        r.Top = wk.Top + 4;
                        r.Bottom = r.Top + newHVert;
                    }
                    else if (anchorMarkerCenterY > 0)
                    {
                        int half = newHVert / 2;
                        r.Top = anchorMarkerCenterY - half;
                        r.Bottom = r.Top + newHVert;

                        if (r.Top < wk.Top + 4)
                        {
                            int d = (wk.Top + 4) - r.Top;
                            r.Top += d;
                            r.Bottom += d;
                        }
                        if (r.Bottom > wk.Bottom - 4)
                        {
                            int d = r.Bottom - (wk.Bottom - 4);
                            r.Top -= d;
                            r.Bottom -= d;
                        }
                    }

                    Marshal.StructureToPtr(r, m.LParam, true);
                    m.Result = (IntPtr)1;
                    return;
                }
            }
            if (m.Msg == WM_GETMINMAXINFO)
            {
                MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(m.LParam, typeof(MINMAXINFO));
                if (IsHorizontalDockMode)
                {
                    mmi.ptMinTrackSize = new Point(480, 160);
                    mmi.ptMaxTrackSize = new Point(1920, 600);
                }
                else
                {
                    mmi.ptMinTrackSize = new Point(310, 420);
                    mmi.ptMaxTrackSize = new Point(1600, 1400);
                }
                Marshal.StructureToPtr(mmi, m.LParam, true);
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutControls();
        }

        private static void DrawFolderIcon(Graphics g, Rectangle rect, Color col)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(col, 1.2f))
            {
                int x = rect.X;
                int y = rect.Y;
                int w = rect.Width;
                int h = rect.Height;

                Point[] pts = new Point[]
                {
                    new Point(x, y + 2),
                    new Point(x + 4, y + 2),
                    new Point(x + 6, y),
                    new Point(x + w, y),
                    new Point(x + w, y + h),
                    new Point(x, y + h)
                };
                g.DrawPolygon(pen, pts);
                g.DrawLine(pen, x, y + 4, x + w, y + 4);
            }
        }

        private void OnSubTabsMouseWheel(object sender, MouseEventArgs e)
        {
            if (flpSubTabs == null || pnlSubTabsClip == null) return;
            int maxScroll = Math.Max(0, flpSubTabs.Width - pnlSubTabsClip.Width);
            if (maxScroll <= 0)
            {
                subTabsScrollX = 0;
            }
            else
            {
                subTabsScrollX += (e.Delta > 0 ? 36 : -36);
                if (subTabsScrollX > 0) subTabsScrollX = 0;
                if (subTabsScrollX < -maxScroll) subTabsScrollX = -maxScroll;
            }
            flpSubTabs.Left = subTabsScrollX;
            UpdateScrollButtonsVisibility();
        }

        private void UpdateScrollButtonsVisibility()
        {
            if (btnSubTabsScrollLeft != null) btnSubTabsScrollLeft.Visible = false;
            if (btnSubTabsScrollRight != null) btnSubTabsScrollRight.Visible = false;
        }

        private void OnFoldersMouseWheel(object sender, MouseEventArgs e)
        {
            if (flpFolders == null || pnlFolders == null) return;
            int totalW = 0;
            foreach (Control c in flpFolders.Controls)
            {
                if (c.Visible) totalW = Math.Max(totalW, c.Right);
            }
            int availW = pnlFolders.Width - 16;
            int maxScroll = Math.Max(0, totalW - availW);
            if (maxScroll <= 0)
            {
                foldersScrollX = 0;
            }
            else
            {
                foldersScrollX += (e.Delta > 0 ? 32 : -32);
                if (foldersScrollX > 0) foldersScrollX = 0;
                if (foldersScrollX < -maxScroll) foldersScrollX = -maxScroll;
            }
            flpFolders.Left = 8 + foldersScrollX;
        }

        private void UpdateSubTabsGeometry()
        {
            if (flpSubTabs == null || pnlSubTabsClip == null) return;
            int totalTabsW = 0;
            int btnH = 34;

            for (int i = 0; i < filterButtons.Count; i++)
            {
                var b = filterButtons[i];
                int btnW = (i == 1) ? 38 : 34;
                Padding m = (i == 0) ? new Padding(1, 0, 5, 0) : (i == 1 ? new Padding(5, 0, 1, 0) : new Padding(1, 0, 1, 0));
                b.Size = new Size(btnW, btnH);
                b.Margin = m;
                totalTabsW += btnW + m.Left + m.Right;
            }

            flpSubTabs.Width = totalTabsW;
            flpSubTabs.Height = 36;

            int clipW = pnlSubTabsClip.Width;
            if (flpSubTabs.Width <= clipW)
            {
                subTabsScrollX = (clipW - flpSubTabs.Width) / 2;
            }
            else
            {
                subTabsScrollX = 0;
            }
            flpSubTabs.Left = subTabsScrollX;
            UpdateScrollButtonsVisibility();
        }

        private string GetCurrentFilterTitle()
        {
            if (currentFilterIndex == 1) return "Текст";
            if (currentFilterIndex == 2) return "Снимки";
            if (currentFilterIndex == 3) return "Ссылки";
            if (currentFilterIndex == 4) return "Папки";
            if (currentFilterIndex == 5) return "Файлы";
            if (currentFilterIndex == 6) return "Закрепленные";
            return "Все категории";
        }

        private void UpdateSubBarLayout()
        {
            if (pnlSubBar == null || IsCornerDockMode) return;

            int w = this.ClientSize.Width;
            if (!IsHorizontalDockMode) pnlSubBar.Width = w;

            if (lblAppHeader != null)
            {
                lblAppHeader.Text = (currentMainTab == 1) ? "SuperHub" : "Буфер обмена";
                lblAppHeader.Visible = !IsHorizontalDockMode;
                lblAppHeader.Invalidate();
            }
            if (pnlTopBar != null) pnlTopBar.Invalidate();

            // Обновляем подсказку первой кнопки-переключателя режима
            if (filterButtons.Count > 0 && toolTip != null)
            {
                string modeTip = (currentMainTab == 0)
                    ? "Режим: Буфер обмена (нажмите для переключения в SuperHub)"
                    : "Режим: SuperHub (нажмите для переключения в Буфер обмена)";
                toolTip.SetToolTip(filterButtons[0], modeTip);
            }

            int rightX;
            if (IsHorizontalDockMode)
            {
                if (lblSectionTitle != null) lblSectionTitle.Visible = false;

                int actionsCount = (currentMainTab == 1 ? 4 : 2);
                int actionsWidth = actionsCount * 28 + 4;
                pnlSubBar.Width = actionsWidth;

                // Кнопки действий прижимаются к правому краю pnlSubBar (вплотную к системным кнопкам pnlTopBar)
                int curSubX = actionsWidth - 2;

                if (btnClearAll != null)
                {
                    btnClearAll.Visible = true;
                    btnClearAll.Size = new Size(24, 24);
                    btnClearAll.Location = new Point(curSubX - 24, 6);
                    btnClearAll.BringToFront();
                    curSubX -= 28;
                }
                if (btnSearchToggle != null)
                {
                    btnSearchToggle.Visible = true;
                    btnSearchToggle.Size = new Size(24, 24);
                    btnSearchToggle.Location = new Point(curSubX - 24, 6);
                    btnSearchToggle.BringToFront();
                    curSubX -= 28;
                }

                if (currentMainTab == 1)
                {
                    if (btnAddNote != null)
                    {
                        btnAddNote.Visible = true;
                        btnAddNote.Size = new Size(24, 24);
                        btnAddNote.Location = new Point(curSubX - 24, 6);
                        btnAddNote.BringToFront();
                        curSubX -= 28;
                    }
                    if (btnDragMode != null)
                    {
                        btnDragMode.Visible = true;
                        btnDragMode.Size = new Size(24, 24);
                        btnDragMode.Location = new Point(curSubX - 24, 6);
                        btnDragMode.BringToFront();
                        curSubX -= 28;
                    }
                }
                else
                {
                    if (btnAddNote != null) btnAddNote.Visible = false;
                    if (btnDragMode != null) btnDragMode.Visible = false;
                }
                if (btnDropHint != null) btnDropHint.Visible = false;
                if (pnlFolders != null) pnlFolders.Visible = false;
                return;
            }

            rightX = w - 14;

            // 1. Справа иконочная кнопка [🗑️ Очистить все] (26x26)
            if (btnClearAll != null)
            {
                btnClearAll.Visible = true;
                btnClearAll.Size = new Size(26, 26);
                btnClearAll.Location = new Point(rightX - 26, 5);
                btnClearAll.BringToFront();
                rightX -= 30;
            }

            // 2. Кнопка [🔍] Поиск (26x26)
            if (btnSearchToggle != null)
            {
                btnSearchToggle.Visible = true;
                btnSearchToggle.Size = new Size(26, 26);
                btnSearchToggle.Location = new Point(rightX - 26, 5);
                btnSearchToggle.BringToFront();
                rightX -= 30;
            }

            if (currentMainTab == 0) // Буфер обмена
            {
                if (btnDragMode != null) btnDragMode.Visible = false;
                if (btnAddNote != null) btnAddNote.Visible = false;
                if (btnDropHint != null) btnDropHint.Visible = false;
                if (pnlFolders != null) pnlFolders.Visible = false;

                if (lblSectionTitle != null)
                {
                    lblSectionTitle.Text = GetCurrentFilterTitle();
                    lblSectionTitle.Visible = true;
                    lblSectionTitle.Location = new Point(16, 6);
                    lblSectionTitle.Size = new Size(Math.Max(80, rightX - 20), 24);
                }
            }
            else // SuperHub
            {
                if (btnDropHint != null) btnDropHint.Visible = false;

                // Компактные иконочные кнопки [+ Заметка] и [Режим Копия/Перенос/Ярлык] справа перед поиском
                if (btnAddNote != null)
                {
                    btnAddNote.Visible = true;
                    btnAddNote.Size = new Size(26, 26);
                    btnAddNote.Location = new Point(rightX - 26, 5);
                    btnAddNote.BringToFront();
                    rightX -= 30;
                }

                if (btnDragMode != null)
                {
                    btnDragMode.Visible = true;
                    btnDragMode.Size = new Size(26, 26);
                    btnDragMode.Location = new Point(rightX - 26, 5);
                    btnDragMode.BringToFront();
                    rightX -= 30;
                }

                if (lblSectionTitle != null)
                {
                    lblSectionTitle.Text = GetCurrentFilterTitle();
                    lblSectionTitle.Visible = true;
                    lblSectionTitle.Location = new Point(16, 6);
                    lblSectionTitle.Size = new Size(Math.Max(70, rightX - 20), 24);
                }

                if (pnlFolders != null)
                {
                    pnlFolders.Visible = Config.SuperHubFoldersEnabled;
                    pnlFolders.Width = w;
                    if (flpFolders != null)
                    {
                        flpFolders.Width = Math.Max(100, w - 16);
                    }
                }
            }

            if (pnlSubTabsClip != null && pnlMainTabs != null)
            {
                pnlSubTabsClip.Visible = true;
                pnlSubTabsClip.Location = new Point(4, 2);
                pnlSubTabsClip.Size = new Size(Math.Max(100, w - 8), 36);
                UpdateSubTabsGeometry();
            }
        }

        private void LayoutCardInternalControls(Control card, int cardW, int cardH, bool isHoriz)
        {
            if (card == null) return;
            card.Size = new Size(cardW, cardH);

            foreach (Control sub in card.Controls)
            {
                if (sub is Button)
                {
                    string tag = sub.Tag != null ? sub.Tag.ToString() : "";
                    if (tag == "btn_more") { sub.Left = cardW - 30; sub.Top = 6; sub.Size = new Size(24, 22); }
                    else if (tag == "btn_pin") { sub.Left = cardW - 30; sub.Top = cardH - 28; sub.Size = new Size(24, 22); }
                }
                else if (sub is Panel && sub.Tag != null && sub.Tag.ToString() == "img_preview")
                {
                    if (isHoriz)
                    {
                        sub.Location = new Point(6, 6);
                        sub.Size = new Size(Math.Max(50, cardW - 38), Math.Max(40, cardH - 12));
                    }
                    else
                    {
                        sub.Location = new Point(12, 10);
                        sub.Size = new Size(Math.Max(60, cardW - 50), Math.Max(40, cardH - 20));
                    }
                }
                else if (sub is Label)
                {
                    if (isHoriz)
                    {
                        if (card.Tag is ClipboardItem && ((ClipboardItem)card.Tag).Type == ClipboardItemType.Text)
                        {
                            sub.Left = 10;
                            sub.Top = 8;
                            sub.Width = Math.Max(40, cardW - 42);
                            sub.Height = Math.Max(20, cardH - 16);
                        }
                        else if (sub.Top > 25)
                        {
                            sub.Left = 46;
                            sub.Top = 32;
                            sub.Width = Math.Max(30, cardW - 46 - 32);
                        }
                        else
                        {
                            sub.Left = 46;
                            sub.Top = 10;
                            sub.Width = Math.Max(30, cardW - 46 - 32);
                        }
                    }
                    else
                    {
                        if (sub.Top > 25)
                        {
                            sub.Width = Math.Max(30, cardW - sub.Left - 38);
                        }
                        else
                        {
                            sub.Width = Math.Max(30, cardW - sub.Left - 38);
                            if (card.Tag is ClipboardItem && ((ClipboardItem)card.Tag).Type == ClipboardItemType.Text)
                            {
                                sub.Height = Math.Max(20, cardH - 22);
                            }
                        }
                    }
                }
                else if (sub is Panel && sub.Tag == null)
                {
                    if (isHoriz)
                    {
                        sub.Location = new Point(8, Math.Max(6, (cardH - 36) / 2));
                        sub.Size = new Size(32, 32);
                    }
                }
            }
        }

        private void LayoutControls()
        {
            if (pnlTopBar == null) return;
            this.SuspendLayout();

            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;

            if (IsCornerDockMode)
            {
                // Секторный UI 90° (четверть круга из угла экрана, точно как угловой маркер)
                PointF origin;
                float startAngle, sweepAngle, bisectorAngle;
                GetCornerGeometry(w, h, dockPositionMode, out origin, out startAngle, out sweepAngle, out bisectorAngle);

                // Скрываем прямоугольные шапки Win+V режима
                pnlTopBar.Visible = false;
                pnlMainTabs.Visible = false;
                pnlSubBar.Visible = false;
                if (lblAppHeader != null) lblAppHeader.Visible = false;
                if (pnlFolders != null) pnlFolders.Visible = false;

                float scale = Math.Max(0.85f, Math.Min(1.2f, (float)w / 440f));

                // Кнопки в угловом секторе должны быть строго прозрачными без прямоугольных непрозрачных подложек
                if (btnOpenSettings != null) btnOpenSettings.BackColor = Color.Transparent;
                if (btnClose != null) btnClose.BackColor = Color.Transparent;
                if (btnPinWindow != null) btnPinWindow.BackColor = Color.Transparent;
                if (btnClickAction != null) btnClickAction.BackColor = Color.Transparent;
                if (btnClearAll != null) btnClearAll.BackColor = Color.Transparent;
                if (btnSearchToggle != null) btnSearchToggle.BackColor = Color.Transparent;
                if (btnAddNote != null) btnAddNote.BackColor = Color.Transparent;
                if (btnDragMode != null) btnDragMode.BackColor = Color.Transparent;
                for (int i = 0; i < filterButtons.Count; i++)
                {
                    filterButtons[i].BackColor = Color.Transparent;
                }

                // 1. Внутренний сектор: системные кнопки у вершины угла (дуга r = 52)
                float rSystem = 52f * scale;
                PointF ptSet = GetPolarPoint(origin, rSystem, bisectorAngle - 28f);
                PointF ptClose = GetPolarPoint(origin, rSystem, bisectorAngle);
                PointF ptPin = GetPolarPoint(origin, rSystem, bisectorAngle + 28f);

                if (btnOpenSettings != null)
                {
                    if (btnOpenSettings.Parent != this) this.Controls.Add(btnOpenSettings);
                    btnOpenSettings.Size = new Size(22, 22);
                    btnOpenSettings.Location = new Point((int)(ptSet.X - 11), (int)(ptSet.Y - 11));
                    btnOpenSettings.Visible = true;
                    btnOpenSettings.BringToFront();
                }

                if (btnClose != null)
                {
                    if (btnClose.Parent != this) this.Controls.Add(btnClose);
                    btnClose.Size = new Size(22, 22);
                    btnClose.Location = new Point((int)(ptClose.X - 11), (int)(ptClose.Y - 11));
                    btnClose.Visible = true;
                    btnClose.BringToFront();
                }

                if (btnPinWindow != null)
                {
                    if (btnPinWindow.Parent != this) this.Controls.Add(btnPinWindow);
                    btnPinWindow.Size = new Size(22, 22);
                    btnPinWindow.Location = new Point((int)(ptPin.X - 11), (int)(ptPin.Y - 11));
                    btnPinWindow.Visible = true;
                    btnPinWindow.BringToFront();
                }

                // 2. Двойной переключатель режимов: Буфер обмена / SuperHub (дуга r = 96)
                float rMode = 96f * scale;
                PointF ptClip = GetPolarPoint(origin, rMode, bisectorAngle - 16f);
                PointF ptHub = GetPolarPoint(origin, rMode, bisectorAngle + 16f);
                if (btnMainClipboard != null)
                {
                    if (btnMainClipboard.Parent != this) this.Controls.Add(btnMainClipboard);
                    btnMainClipboard.Visible = true;
                    btnMainClipboard.Size = new Size(30, 30);
                    btnMainClipboard.Location = new Point((int)(ptClip.X - 15), (int)(ptClip.Y - 15));
                    btnMainClipboard.BringToFront();
                }
                if (btnMainSuperHub != null)
                {
                    if (btnMainSuperHub.Parent != this) this.Controls.Add(btnMainSuperHub);
                    btnMainSuperHub.Visible = true;
                    btnMainSuperHub.Size = new Size(30, 30);
                    btnMainSuperHub.Location = new Point((int)(ptHub.X - 15), (int)(ptHub.Y - 15));
                    btnMainSuperHub.BringToFront();
                }

                // 3. Быстрые действия (дуга r = 146)
                float rActions = 146f * scale;
                if (currentMainTab == 1) // SuperHub
                {
                    PointF ptClr = GetPolarPoint(origin, rActions, bisectorAngle - 27f);
                    PointF ptSrch = GetPolarPoint(origin, rActions, bisectorAngle - 9f);
                    PointF ptNote = GetPolarPoint(origin, rActions, bisectorAngle + 9f);
                    PointF ptDrag = GetPolarPoint(origin, rActions, bisectorAngle + 27f);

                    if (btnClearAll != null)
                    {
                        if (btnClearAll.Parent != this) this.Controls.Add(btnClearAll);
                        btnClearAll.Size = new Size(24, 24);
                        btnClearAll.Location = new Point((int)(ptClr.X - 12), (int)(ptClr.Y - 12));
                        btnClearAll.Visible = true;
                        btnClearAll.BringToFront();
                    }
                    if (btnSearchToggle != null)
                    {
                        if (btnSearchToggle.Parent != this) this.Controls.Add(btnSearchToggle);
                        btnSearchToggle.Size = new Size(24, 24);
                        btnSearchToggle.Location = new Point((int)(ptSrch.X - 12), (int)(ptSrch.Y - 12));
                        btnSearchToggle.Visible = true;
                        btnSearchToggle.BringToFront();
                    }
                    if (btnAddNote != null)
                    {
                        if (btnAddNote.Parent != this) this.Controls.Add(btnAddNote);
                        btnAddNote.Size = new Size(24, 24);
                        btnAddNote.Location = new Point((int)(ptNote.X - 12), (int)(ptNote.Y - 12));
                        btnAddNote.Visible = true;
                        btnAddNote.BringToFront();
                    }
                    if (btnDragMode != null)
                    {
                        if (btnDragMode.Parent != this) this.Controls.Add(btnDragMode);
                        btnDragMode.Size = new Size(24, 24);
                        btnDragMode.Location = new Point((int)(ptDrag.X - 12), (int)(ptDrag.Y - 12));
                        btnDragMode.Visible = true;
                        btnDragMode.BringToFront();
                    }
                    if (btnClickAction != null) btnClickAction.Visible = false;
                }
                else // Буфер обмена
                {
                    PointF ptClr = GetPolarPoint(origin, rActions, bisectorAngle - 22f);
                    PointF ptSrch = GetPolarPoint(origin, rActions, bisectorAngle);
                    PointF ptAct = GetPolarPoint(origin, rActions, bisectorAngle + 22f);

                    if (btnClearAll != null)
                    {
                        if (btnClearAll.Parent != this) this.Controls.Add(btnClearAll);
                        btnClearAll.Size = new Size(24, 24);
                        btnClearAll.Location = new Point((int)(ptClr.X - 12), (int)(ptClr.Y - 12));
                        btnClearAll.Visible = true;
                        btnClearAll.BringToFront();
                    }
                    if (btnSearchToggle != null)
                    {
                        if (btnSearchToggle.Parent != this) this.Controls.Add(btnSearchToggle);
                        btnSearchToggle.Size = new Size(24, 24);
                        btnSearchToggle.Location = new Point((int)(ptSrch.X - 12), (int)(ptSrch.Y - 12));
                        btnSearchToggle.Visible = true;
                        btnSearchToggle.BringToFront();
                    }
                    if (btnClickAction != null)
                    {
                        if (btnClickAction.Parent != this) this.Controls.Add(btnClickAction);
                        btnClickAction.Size = new Size(24, 24);
                        btnClickAction.Location = new Point((int)(ptAct.X - 12), (int)(ptAct.Y - 12));
                        btnClickAction.Visible = true;
                        btnClickAction.BringToFront();
                    }
                    if (btnAddNote != null) btnAddNote.Visible = false;
                    if (btnDragMode != null) btnDragMode.Visible = false;
                }

                // 4. Дуга категорий / фильтров (дуга r = 196)
                float rFilters = 196f * scale;
                if (filterButtons.Count > 0)
                {
                    filterButtons[0].Visible = false;
                }
                int filterCount = Math.Max(0, filterButtons.Count - 1);
                for (int k = 0; k < filterCount; k++)
                {
                    float fAngle = bisectorAngle - 33f + k * 11f;
                    PointF ptF = GetPolarPoint(origin, rFilters, fAngle);
                    Button b = filterButtons[k + 1];
                    if (b.Parent != this) this.Controls.Add(b);
                    b.Size = new Size(26, 26);
                    b.Location = new Point((int)(ptF.X - 13), (int)(ptF.Y - 13));
                    b.Visible = true;
                    b.BringToFront();
                }

                // 5. Поиск (если активен)
                if (pnlSearch != null)
                {
                    pnlSearch.Visible = isSearchOpen || (txtSearch != null && !string.IsNullOrEmpty(txtSearch.Text));
                    if (pnlSearch.Visible)
                    {
                        PointF ptSrchBar = GetPolarPoint(origin, 236f * scale, bisectorAngle);
                        int sW = 200;
                        pnlSearch.Location = new Point((int)(ptSrchBar.X - sW / 2), (int)(ptSrchBar.Y - 16));
                        pnlSearch.Size = new Size(sW, 32);
                        if (searchBoxBg != null) searchBoxBg.Width = pnlSearch.Width;
                        if (txtSearch != null && searchBoxBg != null) txtSearch.Width = Math.Max(60, searchBoxBg.ClientSize.Width - 36);
                        pnlSearch.BringToFront();
                    }
                }

                // 6. Полноразмерный вьюпорт для дугового размещения карточек (отправляем назад, чтобы не перекрывал кнопки)
                if (pnlViewport != null)
                {
                    pnlViewport.SendToBack();
                    pnlViewport.Location = new Point(0, 0);
                    pnlViewport.Size = new Size(w, h);
                    pnlViewport.BackColor = Color.Transparent;
                    if (cardsContainerClipboard != null)
                    {
                        cardsContainerClipboard.Location = new Point(0, 0);
                        cardsContainerClipboard.Size = new Size(w, h);
                        cardsContainerClipboard.BackColor = Color.Transparent;
                        cardsContainerClipboard.Visible = (currentMainTab == 0);
                    }
                    if (cardsContainerSuperHub != null)
                    {
                        cardsContainerSuperHub.Location = new Point(0, 0);
                        cardsContainerSuperHub.Size = new Size(w, h);
                        cardsContainerSuperHub.BackColor = Color.Transparent;
                        cardsContainerSuperHub.Visible = (currentMainTab == 1);
                    }

                    Panel activeCont = (currentMainTab == 1) ? cardsContainerSuperHub : cardsContainerClipboard;
                    LayoutCornerCards(activeCont, currentMainTab == 1);
                }

                // Гарантируем, что все интерактивные кнопки находятся поверх вьюпорта
                if (btnClose != null && btnClose.Visible) btnClose.BringToFront();
                if (btnOpenSettings != null && btnOpenSettings.Visible) btnOpenSettings.BringToFront();
                if (btnPinWindow != null && btnPinWindow.Visible) btnPinWindow.BringToFront();
                if (btnMainClipboard != null && btnMainClipboard.Visible) btnMainClipboard.BringToFront();
                if (btnMainSuperHub != null && btnMainSuperHub.Visible) btnMainSuperHub.BringToFront();
                if (btnClearAll != null && btnClearAll.Visible) btnClearAll.BringToFront();
                if (btnSearchToggle != null && btnSearchToggle.Visible) btnSearchToggle.BringToFront();
                if (btnAddNote != null && btnAddNote.Visible) btnAddNote.BringToFront();
                if (btnDragMode != null && btnDragMode.Visible) btnDragMode.BringToFront();
                if (btnClickAction != null && btnClickAction.Visible) btnClickAction.BringToFront();
                for (int fb = 1; fb < filterButtons.Count; fb++)
                {
                    if (filterButtons[fb].Visible) filterButtons[fb].BringToFront();
                }
                if (btnDomePrev != null && btnDomePrev.Visible) btnDomePrev.BringToFront();
                if (btnDomeNext != null && btnDomeNext.Visible) btnDomeNext.BringToFront();
                if (pnlSearch != null && pnlSearch.Visible) pnlSearch.BringToFront();

                UpdateCornerRegion();
                this.ResumeLayout(true);
                return;
            }

            // Возврат элементов в родительские контейнеры для обычных режимов
            if (btnClose != null) { btnClose.BackColor = Color.Transparent; if (btnClose.Parent != pnlTopBar) pnlTopBar.Controls.Add(btnClose); }
            if (btnPinWindow != null) { btnPinWindow.BackColor = Color.Transparent; if (btnPinWindow.Parent != pnlTopBar) pnlTopBar.Controls.Add(btnPinWindow); }
            if (btnClickAction != null) { btnClickAction.BackColor = Color.Transparent; if (btnClickAction.Parent != pnlTopBar) pnlTopBar.Controls.Add(btnClickAction); }
            if (btnOpenSettings != null) { btnOpenSettings.BackColor = Color.Transparent; if (btnOpenSettings.Parent != pnlTopBar) pnlTopBar.Controls.Add(btnOpenSettings); }
            if (btnClearAll != null) { btnClearAll.BackColor = Color.Transparent; if (btnClearAll.Parent != pnlSubBar) pnlSubBar.Controls.Add(btnClearAll); }
            if (btnSearchToggle != null) { btnSearchToggle.BackColor = Color.Transparent; if (btnSearchToggle.Parent != pnlSubBar) pnlSubBar.Controls.Add(btnSearchToggle); }
            if (btnAddNote != null) { btnAddNote.BackColor = Color.Transparent; if (btnAddNote.Parent != pnlSubBar) pnlSubBar.Controls.Add(btnAddNote); }
            if (btnDragMode != null) { btnDragMode.BackColor = Color.Transparent; if (btnDragMode.Parent != pnlSubBar) pnlSubBar.Controls.Add(btnDragMode); }
            if (btnMainClipboard != null) btnMainClipboard.Visible = false;
            if (btnMainSuperHub != null) btnMainSuperHub.Visible = false;
            if (pnlCornerEmpty != null) pnlCornerEmpty.Visible = false;
            if (flpSubTabs != null)
            {
                foreach (var b in filterButtons)
                {
                    if (b.Parent != flpSubTabs) flpSubTabs.Controls.Add(b);
                    b.Visible = true;
                }
            }
            pnlTopBar.Visible = true;
            pnlMainTabs.Visible = true;
            pnlSubBar.Visible = true;

            int curY;

            if (IsHorizontalDockMode)
            {
                int headerY = 0;
                int sysBtnsWidth = 108; // 4 системные кнопки справа (104px + зазор)
                int actionsCount = (currentMainTab == 1 ? 4 : 2);
                int actionsWidth = actionsCount * 28 + 4;

                if (lblAppHeader != null)
                {
                    lblAppHeader.Visible = false;
                }

                // Системные кнопки в pnlTopBar:
                if (btnClose != null)
                {
                    btnClose.Visible = true;
                    btnClose.Size = new Size(26, 24);
                    btnClose.Location = new Point(sysBtnsWidth - 30, 6);
                    btnClose.BringToFront();
                }
                if (btnPinWindow != null)
                {
                    btnPinWindow.Visible = true;
                    btnPinWindow.Size = new Size(22, 22);
                    btnPinWindow.Location = new Point(sysBtnsWidth - 54, 7);
                    btnPinWindow.BringToFront();
                }
                if (btnClickAction != null)
                {
                    btnClickAction.Visible = true;
                    btnClickAction.Size = new Size(22, 22);
                    btnClickAction.Location = new Point(sysBtnsWidth - 78, 7);
                    btnClickAction.BringToFront();
                }
                if (btnOpenSettings != null)
                {
                    btnOpenSettings.Visible = true;
                    btnOpenSettings.Size = new Size(22, 22);
                    btnOpenSettings.Location = new Point(sysBtnsWidth - 102, 7);
                    btnOpenSettings.BringToFront();
                }

                // Обычный горизонтальный док (BottomCenter / TopCenter)
                pnlTopBar.Location = new Point(w - sysBtnsWidth, 0);
                pnlTopBar.Size = new Size(sysBtnsWidth, 36);
                pnlTopBar.BackColor = Color.Transparent;
                pnlTopBar.BringToFront();

                if (pnlSubBar != null)
                {
                    pnlSubBar.BackColor = Color.Transparent;
                    pnlSubBar.Location = new Point(w - sysBtnsWidth - actionsWidth, 0);
                    pnlSubBar.Size = new Size(actionsWidth, 36);
                    pnlSubBar.BringToFront();
                    UpdateSubBarLayout();
                }

                int leftMargin = 10;
                int filterTabsWidth = Math.Max(120, w - sysBtnsWidth - actionsWidth - leftMargin - 6);

                if (pnlMainTabs != null)
                {
                    pnlMainTabs.BackColor = Color.Transparent;
                    pnlMainTabs.Location = new Point(leftMargin, 0);
                    pnlMainTabs.Size = new Size(filterTabsWidth, 36);
                    pnlMainTabs.BringToFront();
                    if (pnlSubTabsClip != null)
                    {
                        pnlSubTabsClip.Location = new Point(0, 0);
                        pnlSubTabsClip.Size = new Size(pnlMainTabs.Width, 36);
                    }
                    if (btnMainClipboard != null) btnMainClipboard.Visible = false;
                    if (btnMainSuperHub != null) btnMainSuperHub.Visible = false;
                }

                curY = headerY + 38;
            }
            else
            {
                // Обычный вертикальный системный режим Win+V
                pnlTopBar.Location = new Point(0, 0);
                pnlTopBar.Size = new Size(w, 32);
                pnlTopBar.BackColor = Color.Transparent;

                if (btnOpenSettings != null)
                {
                    btnOpenSettings.Size = new Size(22, 22);
                    btnOpenSettings.Location = new Point(12, 5);
                }
                if (btnClickAction != null)
                {
                    btnClickAction.Visible = true;
                    btnClickAction.Size = new Size(22, 22);
                    btnClickAction.Location = new Point(w - 84, 5);
                }
                if (btnPinWindow != null)
                {
                    btnPinWindow.Size = new Size(22, 22);
                    btnPinWindow.Location = new Point(w - 60, 5);
                }
                if (btnMinimize != null)
                {
                    btnMinimize.Visible = false;
                }
                if (lblAppHeader != null)
                {
                    lblAppHeader.Text = (currentMainTab == 1) ? "SuperHub" : "Буфер обмена";
                    lblAppHeader.Visible = true;
                    lblAppHeader.Location = new Point(86, 4);
                    lblAppHeader.Size = new Size(Math.Max(80, w - 172), 24);
                }
                if (btnClose != null)
                {
                    btnClose.Size = new Size(26, 24);
                    btnClose.Location = new Point(w - 34, 4);
                }

                // 2. Панель плоских иконок категорий (как в Win+V Windows 11) - 40px
                if (pnlMainTabs != null)
                {
                    pnlMainTabs.BackColor = Color.Transparent;
                    pnlMainTabs.Location = new Point(0, 32);
                    pnlMainTabs.Size = new Size(w, 40);
                    if (pnlSubTabsClip != null)
                    {
                        pnlSubTabsClip.Location = new Point(4, 2);
                        pnlSubTabsClip.Size = new Size(Math.Max(100, w - 8), 36);
                    }
                    if (btnMainClipboard != null) btnMainClipboard.Visible = false;
                    if (btnMainSuperHub != null) btnMainSuperHub.Visible = false;
                }

                // 3. Строка названия фильтра и иконочных кнопок действий (SubBar) - 36px
                if (pnlSubBar != null)
                {
                    pnlSubBar.BackColor = Color.Transparent;
                    pnlSubBar.Location = new Point(0, 72);
                    pnlSubBar.Size = new Size(w, 36);
                    UpdateSubBarLayout();
                }

                curY = 108;
            }

            // Панель папок SuperHub
            if (pnlFolders != null)
            {
                bool showFolders = (currentMainTab == 1 && Config.SuperHubFoldersEnabled);
                pnlFolders.Visible = showFolders;
                if (showFolders)
                {
                    pnlFolders.Location = new Point(0, curY);
                    pnlFolders.Size = new Size(w, 32);
                    if (flpFolders != null) flpFolders.Width = w - 16;
                    curY += 32;
                }
            }

            // 4. Поиск (Search Bar)
            if (pnlSearch != null)
            {
                pnlSearch.Visible = isSearchOpen || (txtSearch != null && !string.IsNullOrEmpty(txtSearch.Text));
                if (pnlSearch.Visible)
                {
                    pnlSearch.Location = new Point(14, curY);
                    pnlSearch.Size = new Size(w - 28, 32);
                    if (searchBoxBg != null)
                    {
                        searchBoxBg.Width = pnlSearch.Width;
                    }
                    if (txtSearch != null && searchBoxBg != null)
                    {
                        txtSearch.Width = Math.Max(60, searchBoxBg.ClientSize.Width - 36);
                    }
                    curY += 36;
                }
            }

            // 5. Область просмотра карточек
            int contentTop = curY;
            if (pnlViewport != null)
            {
                if (btnDomePrev != null) btnDomePrev.Visible = false;
                if (btnDomeNext != null) btnDomeNext.Visible = false;
                pnlViewport.Location = new Point(0, contentTop);
                pnlViewport.Size = new Size(w, Math.Max(50, h - contentTop - 4));

                if (IsHorizontalDockMode)
                {
                    ApplyFilterToContainer(cardsContainerClipboard, false);
                    ApplyFilterToContainer(cardsContainerSuperHub, true);
                }
                else
                {
                    int cardMargin = 14;
                    int cardW = Math.Max(100, pnlViewport.ClientSize.Width - cardMargin * 2);

                    Action<Panel> updateContainerLayout = (pnl) => {
                        if (pnl == null) return;
                        pnl.Location = new Point(cardMargin, pnl.Top);
                        pnl.Width = cardW;
                        foreach (Control c in pnl.Controls)
                        {
                            if (!(c is Panel)) continue;
                            LayoutCardInternalControls(c, cardW, c.Height, false);
                            c.Invalidate(true);
                        }
                        pnl.Invalidate(true);
                    };

                    if (cardW != lastLayoutCardWidth)
                    {
                        lastLayoutCardWidth = cardW;
                        updateContainerLayout(cardsContainerClipboard);
                        updateContainerLayout(cardsContainerSuperHub);
                        pnlViewport.Invalidate(true);
                    }
                }
                UpdateContainerScroll();
            }

            if (IsCornerDockMode)
            {
                UpdateCornerRegion();
            }
            else
            {
                this.Region = null;
            }

            this.ResumeLayout(true);
        }

        private void DragWindow()
        {
            try
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_CAPTION, IntPtr.Zero);
            }
            catch {}
        }

        private static void DrawTopLeftSystemBoxIcon(Graphics g, Rectangle r, Color col)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(col, 1.2f))
            {
                int cx = r.X + r.Width / 2;
                int cy = r.Y + r.Height / 2;
                g.DrawRectangle(pen, cx - 5, cy - 4, 10, 8);
                g.DrawLine(pen, cx - 5, cy - 1, cx + 5, cy - 1);
                g.DrawLine(pen, cx - 1, cy - 1, cx - 1, cy + 4);
            }
        }

        private static void DrawMoveModeIcon(Graphics g, Rectangle r, Color col)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            using (var pen = new Pen(col, 1.4f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                // Контур файла слева и стрелка переноса вправо
                g.DrawLine(pen, cx - 5.5f, cy - 5f, cx - 1f, cy - 5f);
                g.DrawLine(pen, cx - 5.5f, cy - 5f, cx - 5.5f, cy + 5f);
                g.DrawLine(pen, cx - 5.5f, cy + 5f, cx - 1f, cy + 5f);

                g.DrawLine(pen, cx - 2.5f, cy, cx + 5.5f, cy);
                g.DrawLine(pen, cx + 2f, cy - 3.5f, cx + 5.5f, cy);
                g.DrawLine(pen, cx + 2f, cy + 3.5f, cx + 5.5f, cy);
            }
        }

        private static void DrawAddNoteIcon(Graphics g, Rectangle r, Color col)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            using (var pen = new Pen(col, 1.35f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                // Страница заметки
                g.DrawLine(pen, cx - 5.5f, cy - 5.5f, cx + 2.5f, cy - 5.5f);
                g.DrawLine(pen, cx - 5.5f, cy - 5.5f, cx - 5.5f, cy + 5.5f);
                g.DrawLine(pen, cx - 5.5f, cy + 5.5f, cx + 0.5f, cy + 5.5f);
                g.DrawLine(pen, cx + 2.5f, cy - 5.5f, cx + 2.5f, cy - 1.5f);

                // Строки текста внутри заметки
                g.DrawLine(pen, cx - 3.2f, cy - 2.2f, cx + 0.2f, cy - 2.2f);
                g.DrawLine(pen, cx - 3.2f, cy + 0.8f, cx - 0.8f, cy + 0.8f);

                // Знак [+] в правом нижнем углу
                g.DrawLine(pen, cx + 3.5f, cy + 1f, cx + 3.5f, cy + 6f);
                g.DrawLine(pen, cx + 1f, cy + 3.5f, cx + 6f, cy + 3.5f);
            }
        }

        private static void DrawCategoryFlatIcon(Graphics g, Rectangle r, int btnIdx, Color col, bool isSuperHubMode)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f - 1f;

            using (var pen = new Pen(col, 1.4f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                if (btnIdx == 0) // Переключатель режима: Буфер обмена (планшет) <-> SuperHub (куб)
                {
                    if (!isSuperHubMode)
                    {
                        using (var boardPath = CreateRoundedPath(new Rectangle((int)(cx - 6), (int)(cy - 6), 12, 14), 2))
                        {
                            g.DrawPath(pen, boardPath);
                        }
                        g.DrawRectangle(pen, cx - 3f, cy - 8f, 6f, 3f);
                        g.DrawLine(pen, cx - 3f, cy - 1f, cx + 3f, cy - 1f);
                        g.DrawLine(pen, cx - 3f, cy + 3f, cx + 2f, cy + 3f);
                    }
                    else
                    {
                        PointF top = new PointF(cx, cy - 7f);
                        PointF right = new PointF(cx + 6.5f, cy - 3.5f);
                        PointF bottom = new PointF(cx, cy);
                        PointF left = new PointF(cx - 6.5f, cy - 3.5f);
                        PointF bLeft = new PointF(cx - 6.5f, cy + 4f);
                        PointF bCenter = new PointF(cx, cy + 7.5f);
                        PointF bRight = new PointF(cx + 6.5f, cy + 4f);

                        g.DrawPolygon(pen, new PointF[] { top, right, bottom, left });
                        g.DrawLines(pen, new PointF[] { left, bLeft, bCenter, bRight, right });
                        g.DrawLine(pen, bottom, bCenter);
                    }
                }
                else if (btnIdx == 2) // Текст (filterIdx == 1)
                {
                    g.DrawLine(pen, cx - 6f, cy - 5f, cx + 6f, cy - 5f);
                    g.DrawLine(pen, cx - 6f, cy - 1f, cx + 6f, cy - 1f);
                    g.DrawLine(pen, cx - 6f, cy + 3f, cx + 3f, cy + 3f);
                    g.DrawLine(pen, cx - 6f, cy + 7f, cx + 1f, cy + 7f);
                }
                else if (btnIdx == 3) // Снимки (filterIdx == 2)
                {
                    using (var imgPath = CreateRoundedPath(new Rectangle((int)(cx - 7), (int)(cy - 6), 14, 12), 2))
                    {
                        g.DrawPath(pen, imgPath);
                    }
                    g.DrawEllipse(pen, cx - 4.2f, cy - 3.5f, 2.6f, 2.6f);
                    PointF[] mountain = new PointF[] {
                        new PointF(cx - 5.5f, cy + 5f),
                        new PointF(cx - 1f, cy + 0.5f),
                        new PointF(cx + 2f, cy + 3.5f),
                        new PointF(cx + 4f, cy + 1.5f),
                        new PointF(cx + 6.5f, cy + 5f)
                    };
                    g.DrawLines(pen, mountain);
                }
                else if (btnIdx == 4) // Ссылки (filterIdx == 3)
                {
                    g.DrawArc(pen, cx - 7f, cy - 3.5f, 7f, 7f, 90, 180);
                    g.DrawLine(pen, cx - 3.5f, cy - 3.5f, cx - 1f, cy - 3.5f);
                    g.DrawLine(pen, cx - 3.5f, cy + 3.5f, cx - 1f, cy + 3.5f);

                    g.DrawArc(pen, cx, cy - 3.5f, 7f, 7f, 270, 180);
                    g.DrawLine(pen, cx + 1f, cy - 3.5f, cx + 3.5f, cy - 3.5f);
                    g.DrawLine(pen, cx + 1f, cy + 3.5f, cx + 3.5f, cy + 3.5f);

                    g.DrawLine(pen, cx - 2.5f, cy, cx + 2.5f, cy);
                }
                else if (btnIdx == 5) // Папки (filterIdx == 4)
                {
                    DrawFolderIcon(g, new Rectangle((int)(cx - 7), (int)(cy - 5), 14, 11), col);
                }
                else if (btnIdx == 6) // Файлы (filterIdx == 5 — лист документа со срезанным уголком)
                {
                    PointF[] docOutline = new PointF[] {
                        new PointF(cx - 5f, cy - 6.5f),
                        new PointF(cx + 1.5f, cy - 6.5f),
                        new PointF(cx + 5f, cy - 3f),
                        new PointF(cx + 5f, cy + 6.5f),
                        new PointF(cx - 5f, cy + 6.5f)
                    };
                    g.DrawPolygon(pen, docOutline);
                    g.DrawLines(pen, new PointF[] {
                        new PointF(cx + 1.5f, cy - 6.5f),
                        new PointF(cx + 1.5f, cy - 3f),
                        new PointF(cx + 5f, cy - 3f)
                    });
                }
                else if (btnIdx == 7) // Закрепленные (filterIdx == 6 — наклонная булавка в конце)
                {
                    DrawWin11PinIcon(g, new Rectangle((int)(cx - 10), (int)(cy - 10), 20, 20), col, false);
                }
            }
        }

        private void BuildUI()
        {
            Logger.Log("BuildUI: start");
            try
            {
                this.SuspendLayout();

            // 1. Верхний системный бар Windows 11 (TopBar) - 32px
            pnlTopBar = new FlyoutTopBarPanel
            {
                Location = new Point(0, 0),
                Size = new Size(this.Width, 32),
                BackColor = Color.Transparent,
                Cursor = Cursors.SizeAll
            };
            pnlTopBar.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) DragWindow(); };

            lblAppHeader = new Label
            {
                Text = "Буфер обмена",
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = ThemeHelper.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.SizeAll,
                Visible = true,
                Location = new Point(86, 4),
                Size = new Size(Math.Max(80, this.Width - 172), 24)
            };
            lblAppHeader.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) DragWindow(); };
            pnlTopBar.Controls.Add(lblAppHeader);

            // Левая верхняя кнопка настроек — чистая иконка шестеренки без обводки
            btnOpenSettings = new Button
            {
                Location = new Point(12, 5),
                Size = new Size(22, 22),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnOpenSettings.FlatAppearance.BorderSize = 0;
            btnOpenSettings.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnOpenSettings.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool isGearHover = false;
            btnOpenSettings.MouseEnter += (s, e) => { isGearHover = true; btnOpenSettings.Invalidate(); };
            btnOpenSettings.MouseLeave += (s, e) => { isGearHover = false; btnOpenSettings.Invalidate(); };
            btnOpenSettings.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                ThemeHelper.DrawAcrylicBackground(g, btnOpenSettings, this, acrylicBackdrop);
                if (isGearHover)
                {
                    Rectangle r = new Rectangle(0, 0, btnOpenSettings.Width - 1, btnOpenSettings.Height - 1);
                    using (var path = CreateRoundedPath(r, 4))
                    using (var b = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                    {
                        g.FillPath(b, path);
                    }
                }
                DrawGearIcon(g, new Rectangle(0, 0, btnOpenSettings.Width, btnOpenSettings.Height), isGearHover ? ThemeHelper.TextPrimary : ThemeHelper.TextSecondary);
            };
            btnOpenSettings.Click += (s, e) => { Program.ShowMainForm(); };
            toolTip.SetToolTip(btnOpenSettings, "Открыть настройки программы");
            pnlTopBar.Controls.Add(btnOpenSettings);

            // Кнопка закрепить окно на экране (Pin Window) 📌
            btnPinWindow = new Button
            {
                Location = new Point(38, 6),
                Size = new Size(22, 22),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnPinWindow.FlatAppearance.BorderSize = 0;
            btnPinWindow.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnPinWindow.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool isPinHover = false;
            btnPinWindow.MouseEnter += (s, e) => { isPinHover = true; btnPinWindow.Invalidate(); };
            btnPinWindow.MouseLeave += (s, e) => { isPinHover = false; btnPinWindow.Invalidate(); };
            btnPinWindow.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                ThemeHelper.DrawAcrylicBackground(g, btnPinWindow, this, acrylicBackdrop);
                if (isPinHover)
                {
                    using (var b = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                    using (var p = CreateRoundedPath(new Rectangle(0, 0, btnPinWindow.Width - 1, btnPinWindow.Height - 1), 4))
                    {
                        g.FillPath(b, p);
                    }
                }
                Color pinColor = isWindowPinned ? ThemeHelper.Accent : (isPinHover ? ThemeHelper.TextPrimary : ThemeHelper.TextSecondary);
                DrawWin11PinIcon(g, new Rectangle(0, 0, btnPinWindow.Width, btnPinWindow.Height), pinColor, isWindowPinned);
            };
            btnPinWindow.Click += (s, e) => {
                isWindowPinned = !isWindowPinned;
                btnPinWindow.Invalidate();
                toolTip.SetToolTip(btnPinWindow, isWindowPinned ? "Окно закреплено (кликните, чтобы открепить)" : "Закрепить окно поверх всех окон");
            };
            toolTip.SetToolTip(btnPinWindow, "Закрепить окно поверх всех окон");
            pnlTopBar.Controls.Add(btnPinWindow);

            // Кнопка режима клика по карточке [📥 Вставить / 📋 Копия] в верхней панели
            btnClickAction = new Button
            {
                Location = new Point(64, 6),
                Size = new Size(22, 22),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnClickAction.FlatAppearance.BorderSize = 0;
            btnClickAction.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnClickAction.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool isClickActionHover = false;
            btnClickAction.MouseEnter += (s, e) => { isClickActionHover = true; btnClickAction.Invalidate(); };
            btnClickAction.MouseLeave += (s, e) => { isClickActionHover = false; btnClickAction.Invalidate(); };
            btnClickAction.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                ThemeHelper.DrawAcrylicBackground(g, btnClickAction, this, acrylicBackdrop);
                bool isPaste = string.Equals(Config.ClipboardClickAction, "Paste", StringComparison.OrdinalIgnoreCase);
                if (isClickActionHover)
                {
                    using (var b = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                    using (var p = CreateRoundedPath(new Rectangle(0, 0, btnClickAction.Width - 1, btnClickAction.Height - 1), 4))
                    {
                        g.FillPath(b, p);
                    }
                }
                Rectangle iconRect = new Rectangle(2, 2, btnClickAction.Width - 4, btnClickAction.Height - 4);
                Color icCol = isClickActionHover ? ThemeHelper.TextPrimary : (isPaste ? ThemeHelper.Accent : ThemeHelper.TextSecondary);
                if (isPaste) DrawPasteActionIcon(g, iconRect, icCol);
                else DrawCopyIcon(g, iconRect, icCol);
            };
            btnClickAction.Click += (s, e) => {
                bool isPaste = string.Equals(Config.ClipboardClickAction, "Paste", StringComparison.OrdinalIgnoreCase);
                Config.ClipboardClickAction = isPaste ? "Copy" : "Paste";
                btnClickAction.Invalidate();
                string tip = string.Equals(Config.ClipboardClickAction, "Paste", StringComparison.OrdinalIgnoreCase)
                    ? "Клик по карточке: Вставить сразу в активное окно [Ctrl+V]"
                    : "Клик по карточке: Только скопировать в буфер";
                toolTip.SetToolTip(btnClickAction, tip);
            };
            toolTip.SetToolTip(btnClickAction, string.Equals(Config.ClipboardClickAction, "Paste", StringComparison.OrdinalIgnoreCase)
                ? "Клик по карточке: Вставить сразу в активное окно [Ctrl+V]"
                : "Клик по карточке: Только скопировать в буфер");
            pnlTopBar.Controls.Add(btnClickAction);

            // Кнопка свернуть (скрыта в режиме Win11)
            btnMinimize = new Button
            {
                Location = new Point(this.Width - 62, 4),
                Size = new Size(26, 24),
                FlatStyle = FlatStyle.Flat,
                Visible = false,
                TabStop = false
            };
            pnlTopBar.Controls.Add(btnMinimize);

            // Кнопка закрытия ✕ справа вверху
            btnClose = new Button
            {
                Location = new Point(this.Width - 34, 4),
                Size = new Size(26, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnClose.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool isCloseHover = false;
            btnClose.MouseEnter += (s, e) => { isCloseHover = true; btnClose.Invalidate(); };
            btnClose.MouseLeave += (s, e) => { isCloseHover = false; btnClose.Invalidate(); };
            btnClose.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                ThemeHelper.DrawAcrylicBackground(g, btnClose, this, acrylicBackdrop);
                if (isCloseHover)
                {
                    using (var b = new SolidBrush(Color.FromArgb(196, 43, 28)))
                    using (var p = CreateRoundedPath(new Rectangle(0, 0, btnClose.Width - 1, btnClose.Height - 1), 4))
                    {
                        g.FillPath(b, p);
                    }
                }
                DrawCloseIcon(g, new Rectangle(0, 0, btnClose.Width, btnClose.Height), isCloseHover ? Color.White : ThemeHelper.TextSecondary);
            };
            btnClose.Click += (s, e) => CloseFlyout();
            toolTip.SetToolTip(btnClose, "Закрыть (Esc)");
            pnlTopBar.Controls.Add(btnClose);

            this.Controls.Add(pnlTopBar);

            // 2. Панель плоских иконок категорий Windows 11 (MainTabs) - 40px
            pnlMainTabs = new AcrylicPanel
            {
                Location = new Point(0, 32),
                Size = new Size(this.Width, 40),
                BackColor = Color.Transparent,
                Cursor = Cursors.SizeAll
            };
            pnlMainTabs.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) DragWindow(); };

            btnMainClipboard = new Button
            {
                Visible = false,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnMainClipboard.FlatAppearance.BorderSize = 0;
            btnMainClipboard.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnMainClipboard.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool isClipHover = false;
            btnMainClipboard.MouseEnter += (s, e) => { isClipHover = true; btnMainClipboard.Invalidate(); };
            btnMainClipboard.MouseLeave += (s, e) => { isClipHover = false; btnMainClipboard.Invalidate(); };
            btnMainClipboard.Click += (s, e) => SwitchMainTab(0);
            btnMainClipboard.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                bool isActive = (currentMainTab == 0);
                Rectangle r = new Rectangle(0, 0, btnMainClipboard.Width, btnMainClipboard.Height);
                if (IsCornerDockMode)
                {
                    Color bgCol = isActive 
                        ? ThemeHelper.Accent
                        : (isClipHover ? Color.FromArgb(50, 255, 255, 255) : Color.FromArgb(24, 255, 255, 255));
                    Color borderCol = isActive
                        ? Color.FromArgb(160, 255, 255, 255)
                        : (isClipHover ? Color.FromArgb(85, 255, 255, 255) : Color.FromArgb(38, 255, 255, 255));
                    using (var path = CreateRoundedPath(new Rectangle(1, 1, r.Width - 2, r.Height - 2), r.Width / 2))
                    using (var br = new SolidBrush(bgCol))
                    using (var pen = new Pen(borderCol, 1.2f))
                    {
                        g.FillPath(br, path);
                        g.DrawPath(pen, path);
                    }
                    Color contentCol = isActive ? Color.White : (isClipHover ? ThemeHelper.TextPrimary : ThemeHelper.TextSecondary);
                    int iconSize = 15;
                    int iconX = (r.Width - iconSize) / 2;
                    int iconY = (r.Height - iconSize) / 2;
                    DrawCategoryFlatIcon(g, new Rectangle(iconX, iconY, iconSize, iconSize), 0, contentCol, false);
                    return;
                }
                Color normBg = isActive 
                    ? Color.FromArgb(220, ThemeHelper.Accent.R, ThemeHelper.Accent.G, ThemeHelper.Accent.B)
                    : (isClipHover ? Color.FromArgb(48, 255, 255, 255) : Color.FromArgb(20, 255, 255, 255));
                Color normBorder = isActive
                    ? Color.FromArgb(100, 255, 255, 255)
                    : (isClipHover ? Color.FromArgb(64, 255, 255, 255) : Color.FromArgb(32, 255, 255, 255));
                using (var path = CreateRoundedPath(new Rectangle(1, 1, r.Width - 2, r.Height - 2), 6))
                using (var br = new SolidBrush(normBg))
                using (var pen = new Pen(normBorder, 1f))
                {
                    g.FillPath(br, path);
                    g.DrawPath(pen, path);
                }
                Color normContent = isActive ? Color.White : (isClipHover ? ThemeHelper.TextPrimary : ThemeHelper.TextSecondary);
                int normIconSize = 13;
                int normIconX = 7;
                int normIconY = (r.Height - normIconSize) / 2;
                DrawCategoryFlatIcon(g, new Rectangle(normIconX, normIconY, normIconSize, normIconSize), 0, normContent, false);

                using (var f = new Font("Segoe UI Semibold", 8.25f))
                using (var b = new SolidBrush(normContent))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    g.DrawString("Буфер", f, b, new RectangleF(normIconX + normIconSize + 4, 0, r.Width - (normIconX + normIconSize + 4), r.Height), sf);
                }
            };
            toolTip.SetToolTip(btnMainClipboard, "Буфер обмена (Win+V)");
            this.Controls.Add(btnMainClipboard);

            btnMainSuperHub = new Button
            {
                Visible = false,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnMainSuperHub.FlatAppearance.BorderSize = 0;
            btnMainSuperHub.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnMainSuperHub.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool isHubHover = false;
            btnMainSuperHub.MouseEnter += (s, e) => { isHubHover = true; btnMainSuperHub.Invalidate(); };
            btnMainSuperHub.MouseLeave += (s, e) => { isHubHover = false; btnMainSuperHub.Invalidate(); };
            btnMainSuperHub.Click += (s, e) => SwitchMainTab(1);
            btnMainSuperHub.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                bool isActive = (currentMainTab == 1);
                Rectangle r = new Rectangle(0, 0, btnMainSuperHub.Width, btnMainSuperHub.Height);
                if (IsCornerDockMode)
                {
                    Color bgCol = isActive 
                        ? ThemeHelper.SuperHubSecondaryAccent
                        : (isHubHover ? Color.FromArgb(50, 255, 255, 255) : Color.FromArgb(24, 255, 255, 255));
                    Color borderCol = isActive
                        ? Color.FromArgb(160, 255, 255, 255)
                        : (isHubHover ? Color.FromArgb(85, 255, 255, 255) : Color.FromArgb(38, 255, 255, 255));
                    using (var path = CreateRoundedPath(new Rectangle(1, 1, r.Width - 2, r.Height - 2), r.Width / 2))
                    using (var br = new SolidBrush(bgCol))
                    using (var pen = new Pen(borderCol, 1.2f))
                    {
                        g.FillPath(br, path);
                        g.DrawPath(pen, path);
                    }
                    Color contentCol = isActive ? Color.White : (isHubHover ? ThemeHelper.TextPrimary : ThemeHelper.TextSecondary);
                    int iconSize = 15;
                    int iconX = (r.Width - iconSize) / 2;
                    int iconY = (r.Height - iconSize) / 2;
                    DrawCategoryFlatIcon(g, new Rectangle(iconX, iconY, iconSize, iconSize), 0, contentCol, true);
                    return;
                }
                Color normBg = isActive 
                    ? Color.FromArgb(220, ThemeHelper.SuperHubSecondaryAccent.R, ThemeHelper.SuperHubSecondaryAccent.G, ThemeHelper.SuperHubSecondaryAccent.B)
                    : (isHubHover ? Color.FromArgb(48, 255, 255, 255) : Color.FromArgb(20, 255, 255, 255));
                Color normBorder = isActive
                    ? Color.FromArgb(100, 255, 255, 255)
                    : (isHubHover ? Color.FromArgb(64, 255, 255, 255) : Color.FromArgb(32, 255, 255, 255));
                using (var path = CreateRoundedPath(new Rectangle(1, 1, r.Width - 2, r.Height - 2), 6))
                using (var br = new SolidBrush(normBg))
                using (var pen = new Pen(normBorder, 1f))
                {
                    g.FillPath(br, path);
                    g.DrawPath(pen, path);
                }
                Color normContent = isActive ? Color.White : (isHubHover ? ThemeHelper.TextPrimary : ThemeHelper.TextSecondary);
                int normIconSize = 13;
                int normIconX = 7;
                int normIconY = (r.Height - normIconSize) / 2;
                DrawCategoryFlatIcon(g, new Rectangle(normIconX, normIconY, normIconSize, normIconSize), 0, normContent, true);

                using (var f = new Font("Segoe UI Semibold", 8f))
                using (var b = new SolidBrush(normContent))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    g.DrawString("SuperHub", f, b, new RectangleF(normIconX + normIconSize + 4, 0, r.Width - (normIconX + normIconSize + 4), r.Height), sf);
                }
            };
            toolTip.SetToolTip(btnMainSuperHub, "Полка SuperHub (Ctrl+Alt+C)");
            this.Controls.Add(btnMainSuperHub);

            pnlSubTabsClip = new AcrylicPanel
            {
                Location = new Point(4, 2),
                Size = new Size(Math.Max(100, this.Width - 8), 36),
                BackColor = Color.Transparent,
                AutoScroll = false
            };

            flpSubTabs = new AcrylicFlowPanel
            {
                Location = new Point(0, 1),
                Size = new Size(this.Width, 36),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                BackColor = Color.Transparent
            };

            for (int i = 0; i < filterNames.Length; i++)
            {
                int btnIdx = i;
                Button btnFilter = new Button
                {
                    Size = new Size(btnIdx == 1 ? 38 : 34, 34),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                    TabStop = false,
                    Margin = (btnIdx == 0) ? new Padding(1, 0, 5, 0) : (btnIdx == 1 ? new Padding(5, 0, 1, 0) : new Padding(1, 0, 1, 0))
                };
                btnFilter.FlatAppearance.BorderSize = 0;
                btnFilter.FlatAppearance.MouseOverBackColor = Color.Transparent;
                btnFilter.FlatAppearance.MouseDownBackColor = Color.Transparent;
                bool isFltHover = false;
                btnFilter.MouseEnter += (s, e) => { isFltHover = true; btnFilter.Invalidate(); };
                btnFilter.MouseLeave += (s, e) => { isFltHover = false; btnFilter.Invalidate(); };
                btnFilter.Paint += (s, e) => {
                    Graphics g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    ThemeHelper.DrawAcrylicBackground(g, btnFilter, this, acrylicBackdrop);

                    bool isModeToggle = (btnIdx == 0);
                    int filterIdx = btnIdx - 1; // 0..6 для кнопок фильтров (btnIdx 1..7)
                    bool isActive = isModeToggle ? true : (currentFilterIndex == filterIdx);

                    // Фон при ховере или для активного переключателя режима (btnIdx == 0)
                    if (isFltHover || isModeToggle)
                    {
                        Rectangle rHover = new Rectangle(1, 2, btnFilter.Width - 3, btnFilter.Height - 7);
                        using (var path = CreateRoundedPath(rHover, 5))
                        using (var brush = new SolidBrush(Color.FromArgb(isFltHover ? 42 : 24, 255, 255, 255)))
                        {
                            g.FillPath(brush, path);
                        }
                    }

                    Color iconCol = isModeToggle
                        ? (currentMainTab == 1 ? ThemeHelper.SuperHubSecondaryAccent : ThemeHelper.TextPrimary)
                        : (isActive
                            ? ThemeHelper.TextPrimary
                            : (isFltHover ? ThemeHelper.TextPrimary : ThemeHelper.TextSecondary));

                    int yOffset = isModeToggle ? 4 : 0;
                    if (btnIdx == 1) // Фильтр "Все" (filterIdx == 0)
                    {
                        using (var f = new Font("Segoe UI Semibold", 8.75f))
                        using (var b = new SolidBrush(iconCol))
                        {
                            var sf = new StringFormat
                            {
                                Alignment = StringAlignment.Center,
                                LineAlignment = StringAlignment.Center
                            };
                            g.DrawString("Все", f, b, new RectangleF(0, 0, btnFilter.Width, btnFilter.Height), sf);
                        }
                    }
                    else
                    {
                        DrawCategoryFlatIcon(g, new Rectangle(0, 0, btnFilter.Width, btnFilter.Height - yOffset), btnIdx, iconCol, currentMainTab == 1);
                    }

                    // Акцентная полоска-индикатор Windows 11 Fluent:
                    // На референсе полоска отображается только под кнопкой текущего режима (btnIdx == 0: Буфер обмена / SuperHub)
                    if (isModeToggle)
                    {
                        int pillW = 16;
                        int pillH = 3;
                        int pillX = (btnFilter.Width - pillW) / 2;
                        int pillY = btnFilter.Height - 4;
                        Color pillCol = (currentMainTab == 1) ? ThemeHelper.SuperHubSecondaryAccent : ThemeHelper.Accent;
                        using (var pillBrush = new SolidBrush(pillCol))
                        using (var pillPath = CreateRoundedPath(new Rectangle(pillX, pillY, pillW, pillH), 1))
                        {
                            g.FillPath(pillBrush, pillPath);
                        }
                    }
                };
                btnFilter.Click += (s, e) => {
                    if (btnIdx == 0)
                    {
                        // Клик по первой кнопке переключает Буфер обмена <-> SuperHub (иконка меняется)
                        SwitchMainTab(currentMainTab == 0 ? 1 : 0);
                    }
                    else
                    {
                        int filterIdx = btnIdx - 1;
                        if (filterIdx != 0 && currentFilterIndex == filterIdx)
                        {
                            // Повторный клик по активной категории возвращает показ всех элементов («Все»)
                            SwitchFilter(0);
                        }
                        else
                        {
                            SwitchFilter(filterIdx);
                        }
                    }
                };
                btnFilter.MouseWheel += OnSubTabsMouseWheel;
                toolTip.SetToolTip(btnFilter, filterNames[btnIdx]);
                filterButtons.Add(btnFilter);
                flpSubTabs.Controls.Add(btnFilter);
            }

            flpSubTabs.Paint += (s, e) => {
                if (filterButtons.Count > 1)
                {
                    Control b0 = filterButtons[0];
                    Control b1 = filterButtons[1];
                    int divX = (b0.Right + b1.Left) / 2;
                    int divY = (flpSubTabs.Height - 16) / 2;
                    using (var p = new Pen(Color.FromArgb(45, 255, 255, 255), 1f))
                    {
                        e.Graphics.DrawLine(p, divX, divY, divX, divY + 16);
                    }
                }
            };

            pnlSubTabsClip.MouseWheel += OnSubTabsMouseWheel;
            flpSubTabs.MouseWheel += OnSubTabsMouseWheel;
            pnlSubTabsClip.Controls.Add(flpSubTabs);
            pnlMainTabs.Controls.Add(pnlSubTabsClip);

            btnSubTabsScrollLeft = new Button { Visible = false };
            btnSubTabsScrollRight = new Button { Visible = false };
            pnlMainTabs.Controls.Add(btnSubTabsScrollLeft);
            pnlMainTabs.Controls.Add(btnSubTabsScrollRight);

            this.Controls.Add(pnlMainTabs);

            // 3. Строка заголовка активного фильтра и иконочных кнопок действий (SubBar) - 36px
            pnlSubBar = new AcrylicPanel
            {
                Location = new Point(0, 72),
                Size = new Size(this.Width, 36),
                BackColor = Color.Transparent
            };

            lblSectionTitle = new Label
            {
                Text = "Все категории",
                Font = new Font("Segoe UI Semibold", 9.75f),
                ForeColor = ThemeHelper.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(16, 6),
                Size = new Size(180, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlSubBar.Controls.Add(lblSectionTitle);

            // Иконочная кнопка режима копирования / переноса / ярлыка для SuperHub (26x26)
            btnDragMode = new Button
            {
                Location = new Point(this.Width - 134, 5),
                Size = new Size(26, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnDragMode.FlatAppearance.BorderSize = 0;
            btnDragMode.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnDragMode.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool isDragModeHover = false;
            btnDragMode.MouseEnter += (s, e) => { isDragModeHover = true; btnDragMode.Invalidate(); };
            btnDragMode.MouseLeave += (s, e) => { isDragModeHover = false; btnDragMode.Invalidate(); };
            btnDragMode.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                ThemeHelper.DrawAcrylicBackground(g, btnDragMode, this, acrylicBackdrop);
                bool isLink = string.Equals(Config.SuperHubDragMode, "Link", StringComparison.OrdinalIgnoreCase);
                bool isCopy = !isLink && string.Equals(Config.SuperHubDragMode, "Copy", StringComparison.OrdinalIgnoreCase);
                Rectangle r = new Rectangle(0, 0, btnDragMode.Width - 1, btnDragMode.Height - 1);
                using (var path = CreateRoundedPath(r, 4))
                {
                    Color bg = Color.FromArgb(isDragModeHover ? 44 : 24, 255, 255, 255);
                    Color border = isCopy
                        ? Color.FromArgb(isDragModeHover ? 90 : 52, 255, 255, 255)
                        : (isLink ? ThemeHelper.SuperHubSecondaryAccent : Color.FromArgb(220, 150, 50));
                    using (var brush = new SolidBrush(bg))
                        e.Graphics.FillPath(brush, path);
                    using (var pen = new Pen(border, 1f))
                        e.Graphics.DrawPath(pen, path);
                }
                Color iconCol = isCopy
                    ? (isDragModeHover ? ThemeHelper.TextPrimary : ThemeHelper.Accent)
                    : (isLink ? ThemeHelper.SuperHubSecondaryAccent : Color.FromArgb(255, 175, 65));
                Rectangle iconRect = new Rectangle(3, 3, 20, 20);
                if (isCopy) DrawCopyIcon(e.Graphics, iconRect, iconCol);
                else if (isLink) DrawLinkIcon(e.Graphics, iconRect, iconCol);
                else DrawMoveModeIcon(e.Graphics, iconRect, iconCol);
            };
            btnDragMode.Click += (s, e) => {
                string mode = Config.SuperHubDragMode;
                if (string.Equals(mode, "Copy", StringComparison.OrdinalIgnoreCase))
                    Config.SuperHubDragMode = "Move";
                else if (string.Equals(mode, "Move", StringComparison.OrdinalIgnoreCase))
                    Config.SuperHubDragMode = "Link";
                else
                    Config.SuperHubDragMode = "Copy";
                UpdateDragModeButtonVisual();
                btnDragMode.Invalidate();
                if (SuperHubDockForm.Instance != null && !SuperHubDockForm.Instance.IsDisposed)
                {
                    SuperHubDockForm.Instance.RefreshItems();
                }
            };
            UpdateDragModeButtonVisual();
            pnlSubBar.Controls.Add(btnDragMode);

            // Иконочная кнопка добавления заметки [+ Текст] для SuperHub (26x26)
            btnAddNote = new Button
            {
                Location = new Point(this.Width - 104, 5),
                Size = new Size(26, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnAddNote.FlatAppearance.BorderSize = 0;
            btnAddNote.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnAddNote.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool isAddNoteHover = false;
            btnAddNote.MouseEnter += (s, e) => { isAddNoteHover = true; btnAddNote.Invalidate(); };
            btnAddNote.MouseLeave += (s, e) => { isAddNoteHover = false; btnAddNote.Invalidate(); };
            btnAddNote.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                ThemeHelper.DrawAcrylicBackground(g, btnAddNote, this, acrylicBackdrop);
                Rectangle r = new Rectangle(0, 0, btnAddNote.Width - 1, btnAddNote.Height - 1);
                using (var path = CreateRoundedPath(r, 4))
                {
                    Color bg = Color.FromArgb(isAddNoteHover ? 44 : 24, 255, 255, 255);
                    Color border = Color.FromArgb(isAddNoteHover ? 90 : 52, 255, 255, 255);
                    using (var brush = new SolidBrush(bg))
                        e.Graphics.FillPath(brush, path);
                    using (var pen = new Pen(border, 1f))
                        e.Graphics.DrawPath(pen, path);
                }
                DrawAddNoteIcon(e.Graphics, new Rectangle(3, 3, 20, 20), isAddNoteHover ? ThemeHelper.TextPrimary : ThemeHelper.TextSecondary);
            };
            btnAddNote.Click += (s, e) => OpenNewNoteDialog();
            toolTip.SetToolTip(btnAddNote, "Создать текстовую заметку (Ctrl+N)");
            pnlSubBar.Controls.Add(btnAddNote);

            btnDropHint = new Button { Visible = false };
            pnlSubBar.Controls.Add(btnDropHint);

            // Кнопка поиска [🔍] (26x26)
            btnSearchToggle = new Button
            {
                Location = new Point(this.Width - 70, 5),
                Size = new Size(26, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnSearchToggle.FlatAppearance.BorderSize = 0;
            btnSearchToggle.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnSearchToggle.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool isSearchHover = false;
            btnSearchToggle.MouseEnter += (s, e) => { isSearchHover = true; btnSearchToggle.Invalidate(); };
            btnSearchToggle.MouseLeave += (s, e) => { isSearchHover = false; btnSearchToggle.Invalidate(); };
            btnSearchToggle.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                ThemeHelper.DrawAcrylicBackground(g, btnSearchToggle, this, acrylicBackdrop);
                if (isSearchHover || isSearchOpen)
                {
                    Rectangle r = new Rectangle(0, 0, btnSearchToggle.Width - 1, btnSearchToggle.Height - 1);
                    using (var path = CreateRoundedPath(r, 4))
                    using (var brush = new SolidBrush(Color.FromArgb(42, 255, 255, 255)))
                    {
                        e.Graphics.FillPath(brush, path);
                    }
                }
                DrawSearchIcon(e.Graphics, new Rectangle(0, 0, btnSearchToggle.Width, btnSearchToggle.Height), (isSearchHover || isSearchOpen) ? ThemeHelper.TextPrimary : ThemeHelper.TextSecondary);
            };
            btnSearchToggle.Click += (s, e) => {
                isSearchOpen = !isSearchOpen;
                btnSearchToggle.Invalidate();
                LayoutControls();
                if (isSearchOpen && txtSearch != null)
                {
                    SetForegroundWindow(this.Handle);
                    this.Activate();
                    txtSearch.Focus();
                }
            };
            toolTip.SetToolTip(btnSearchToggle, "Поиск по буферу и файлам");
            pnlSubBar.Controls.Add(btnSearchToggle);

            // Иконочная кнопка [🗑️ Очистить все] справа (26x26)
            btnClearAll = new Button
            {
                Location = new Point(this.Width - 40, 5),
                Size = new Size(26, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnClearAll.FlatAppearance.BorderSize = 0;
            btnClearAll.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnClearAll.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool isClearHover = false;
            btnClearAll.MouseEnter += (s, e) => { isClearHover = true; btnClearAll.Invalidate(); };
            btnClearAll.MouseLeave += (s, e) => { isClearHover = false; btnClearAll.Invalidate(); };
            btnClearAll.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                ThemeHelper.DrawAcrylicBackground(g, btnClearAll, this, acrylicBackdrop);
                Rectangle r = new Rectangle(0, 0, btnClearAll.Width - 1, btnClearAll.Height - 1);
                if (isConfirmingClear || isClearHover)
                {
                    using (var path = CreateRoundedPath(r, 4))
                    {
                        Color bg = isConfirmingClear
                            ? Color.FromArgb(196, 43, 28)
                            : Color.FromArgb(44, 255, 255, 255);
                        Color border = isConfirmingClear
                            ? Color.FromArgb(230, 50, 40)
                            : Color.FromArgb(80, 255, 255, 255);
                        using (var brush = new SolidBrush(bg))
                        {
                            e.Graphics.FillPath(brush, path);
                        }
                        using (var pen = new Pen(border, 1f))
                        {
                            e.Graphics.DrawPath(pen, path);
                        }
                    }
                }
                Color trashCol = isConfirmingClear
                    ? Color.White
                    : (isClearHover ? ThemeHelper.TextPrimary : ThemeHelper.TextSecondary);
                DrawTrashIcon(e.Graphics, new Rectangle(0, 0, btnClearAll.Width, btnClearAll.Height), trashCol);
            };

            Action resetClearButton = () => {
                isConfirmingClear = false;
                if (clearConfirmTimer != null) clearConfirmTimer.Stop();
                if (toolTip != null && btnClearAll != null)
                {
                    toolTip.SetToolTip(btnClearAll, "Очистить все (кроме закрепленных)");
                }
                if (btnClearAll != null) btnClearAll.Invalidate();
            };

            btnClearAll.Click += (s, e) => {
                if (!isConfirmingClear)
                {
                    isConfirmingClear = true;
                    toolTip.SetToolTip(btnClearAll, "Нажмите еще раз для подтверждения очистки");
                    btnClearAll.Invalidate();
                    if (clearConfirmTimer == null)
                    {
                        clearConfirmTimer = new System.Windows.Forms.Timer();
                        clearConfirmTimer.Interval = 3000;
                        clearConfirmTimer.Tick += (ts, te) => resetClearButton();
                    }
                    clearConfirmTimer.Stop();
                    clearConfirmTimer.Start();
                    return;
                }

                resetClearButton();
                bool isHubTab = (currentMainTab == 1);
                ClipboardHistoryManager.Instance.ClearAll(null, isHubTab);
                RefreshItems();
            };
            toolTip.SetToolTip(btnClearAll, "Очистить все (кроме закрепленных)");
            pnlSubBar.Controls.Add(btnClearAll);

            this.Controls.Add(pnlSubBar);

            // Панель папок SuperHub (вкладки в стиле браузера)
            pnlFolders = new AcrylicPanel
            {
                Location = new Point(0, 108),
                Size = new Size(this.Width, 32),
                BackColor = ThemeHelper.Background,
                Visible = (currentMainTab == 1 && Config.SuperHubFoldersEnabled)
            };

            flpFolders = new AcrylicFlowPanel
            {
                Location = new Point(8, 3),
                Size = new Size(this.Width - 16, 26),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                BackColor = ThemeHelper.Background
            };
            pnlFolders.MouseWheel += OnFoldersMouseWheel;
            flpFolders.MouseWheel += OnFoldersMouseWheel;
            pnlFolders.Controls.Add(flpFolders);
            this.Controls.Add(pnlFolders);
            RebuildFolderTabs();

            // 4. Строка поиска (Search Bar) - 34px
            pnlSearch = new AcrylicPanel
            {
                Location = new Point(0, 108),
                Size = new Size(this.Width, 34),
                BackColor = Color.FromArgb(31, 31, 31),
                Padding = new Padding(12, 2, 12, 4)
            };

            searchBoxBg = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(38, 38, 38),
                Padding = new Padding(8, 4, 8, 4)
            };
            searchBoxBg.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(ThemeHelper.CardBorder, 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, searchBoxBg.Width - 1, searchBoxBg.Height - 1);
                }
                // Четкая векторная лупа в стиле Fluent
                using (var iconPen = new Pen(Color.FromArgb(160, 160, 160), 1.6f))
                {
                    e.Graphics.DrawEllipse(iconPen, 8f, 7f, 9.5f, 9.5f);
                    e.Graphics.DrawLine(iconPen, 15.5f, 14.5f, 19.5f, 18.5f);
                }
            };
            searchBoxBg.MouseDown += (s, e) => {
                SetForegroundWindow(this.Handle);
                this.Activate();
                if (txtSearch != null) txtSearch.Focus();
            };

            txtSearch = new TextBox
            {
                Location = new Point(28, 4),
                Width = Math.Max(100, searchBoxBg.ClientSize.Width - 36),
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(38, 38, 38),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f)
            };
            txtSearch.MouseDown += (s, e) => {
                SetForegroundWindow(this.Handle);
                this.Activate();
                txtSearch.Focus();
            };
            searchDebounceTimer = new System.Windows.Forms.Timer();
            searchDebounceTimer.Interval = 120;
            searchDebounceTimer.Tick += (s, e) => {
                searchDebounceTimer.Stop();
                ApplyFilterToAllContainers();
            };
            txtSearch.TextChanged += (s, e) => {
                searchDebounceTimer.Stop();
                searchDebounceTimer.Start();
            };
            toolTip.SetToolTip(txtSearch, "Поиск по тексту или имени файла в буфере");
            searchBoxBg.Controls.Add(txtSearch);

            pnlSearch.Controls.Add(searchBoxBg);
            this.Controls.Add(pnlSearch);

            // 5. Область просмотра карточек с кастомным Fluent-скроллбаром
            int contentTop = 142;
            pnlViewport = new FlyoutViewportPanel
            {
                Location = new Point(0, contentTop),
                Size = new Size(this.Width, this.ClientSize.Height - contentTop - 4),
                BackColor = Color.FromArgb(31, 31, 31),
                AllowDrop = true
            };

            cardsContainerClipboard = new AcrylicPanel
            {
                Location = new Point(12, 0),
                Width = pnlViewport.Width - 24,
                BackColor = ThemeHelper.Background,
                AutoSize = false,
                Visible = true
            };
            cardsContainerSuperHub = new AcrylicPanel
            {
                Location = new Point(12, 0),
                Width = pnlViewport.Width - 24,
                BackColor = ThemeHelper.Background,
                AutoSize = false,
                Visible = false
            };
            pnlViewport.Controls.Add(cardsContainerClipboard);
            pnlViewport.Controls.Add(cardsContainerSuperHub);
            cardsContainer = cardsContainerClipboard;

            RegisterSuperHubDropTarget(pnlViewport);
            RegisterSuperHubDropTarget(cardsContainerSuperHub);

            // Прокрутка колесиком мыши
            pnlViewport.MouseWheel += OnViewportMouseWheel;
            cardsContainerClipboard.MouseWheel += OnViewportMouseWheel;
            cardsContainerSuperHub.MouseWheel += OnViewportMouseWheel;

            // Отрисовка темного Fluent Scrollbar и метки Resize Grip
            pnlViewport.Paint += RenderFluentScrollbar;
            pnlViewport.Paint += (s, e) => {
                using (var gripPen = new Pen(Color.FromArgb(90, 90, 90), 1.5f))
                {
                    int gx = pnlViewport.Width - 10;
                    int gy = pnlViewport.Height - 10;
                    e.Graphics.DrawLine(gripPen, gx + 6, gy + 2, gx + 2, gy + 6);
                    e.Graphics.DrawLine(gripPen, gx + 6, gy - 2, gx - 2, gy + 6);
                    e.Graphics.DrawLine(gripPen, gx + 6, gy - 6, gx - 6, gy + 6);
                }
            };
            pnlViewport.MouseMove += OnScrollbarMouseMove;
            pnlViewport.MouseDown += OnScrollbarMouseDown;
            pnlViewport.MouseUp += OnScrollbarMouseUp;
            pnlViewport.MouseLeave += (s, e) => {
                if (isHoveringScroll) { isHoveringScroll = false; pnlViewport.Invalidate(); }
            };

            this.Controls.Add(pnlViewport);

            // Кнопки карусели для полукруглого углового режима
            btnDomePrev = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Visible = false,
                Size = new Size(28, 28),
                TabStop = false
            };
            btnDomePrev.FlatAppearance.BorderSize = 0;
            btnDomePrev.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnDomePrev.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool isDomePrevHover = false;
            btnDomePrev.MouseEnter += (s, e) => { isDomePrevHover = true; btnDomePrev.Invalidate(); };
            btnDomePrev.MouseLeave += (s, e) => { isDomePrevHover = false; btnDomePrev.Invalidate(); };
            btnDomePrev.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                bool enabled = btnDomePrev.Enabled;
                Rectangle r = new Rectangle(0, 0, btnDomePrev.Width - 1, btnDomePrev.Height - 1);
                Color bg = isDomePrevHover && enabled
                    ? Color.FromArgb(70, 255, 255, 255)
                    : (enabled ? Color.FromArgb(36, 255, 255, 255) : Color.FromArgb(14, 255, 255, 255));
                using (var path = CreateRoundedPath(r, btnDomePrev.Width / 2))
                using (var br = new SolidBrush(bg))
                using (var pen = new Pen(Color.FromArgb(enabled ? 60 : 20, 255, 255, 255), 1f))
                {
                    g.FillPath(br, path);
                    g.DrawPath(pen, path);
                }
                using (var p = new Pen(enabled ? (isDomePrevHover ? Color.White : Color.FromArgb(220, 220, 220)) : Color.FromArgb(90, 90, 90), 1.8f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    float cx = btnDomePrev.Width / 2f;
                    float cy = btnDomePrev.Height / 2f;
                    g.DrawLine(p, cx + 2f, cy - 5f, cx - 3f, cy);
                    g.DrawLine(p, cx - 3f, cy, cx + 2f, cy + 5f);
                }
            };
            btnDomePrev.Click += (s, e) => {
                if (IsCornerDockMode)
                {
                    ScrollCornerCards(-1);
                    return;
                }
                int step = (pnlViewport != null ? Math.Max(180, pnlViewport.ClientSize.Width - 20) : 240);
                SetScrollX(scrollX - step);
            };
            toolTip.SetToolTip(btnDomePrev, "Предыдущие карточки");
            this.Controls.Add(btnDomePrev);

            btnDomeNext = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Visible = false,
                Size = new Size(28, 28),
                TabStop = false
            };
            btnDomeNext.FlatAppearance.BorderSize = 0;
            btnDomeNext.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnDomeNext.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool isDomeNextHover = false;
            btnDomeNext.MouseEnter += (s, e) => { isDomeNextHover = true; btnDomeNext.Invalidate(); };
            btnDomeNext.MouseLeave += (s, e) => { isDomeNextHover = false; btnDomeNext.Invalidate(); };
            btnDomeNext.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                bool enabled = btnDomeNext.Enabled;
                Rectangle r = new Rectangle(0, 0, btnDomeNext.Width - 1, btnDomeNext.Height - 1);
                Color bg = isDomeNextHover && enabled
                    ? Color.FromArgb(70, 255, 255, 255)
                    : (enabled ? Color.FromArgb(36, 255, 255, 255) : Color.FromArgb(14, 255, 255, 255));
                using (var path = CreateRoundedPath(r, btnDomeNext.Width / 2))
                using (var br = new SolidBrush(bg))
                using (var pen = new Pen(Color.FromArgb(enabled ? 60 : 20, 255, 255, 255), 1f))
                {
                    g.FillPath(br, path);
                    g.DrawPath(pen, path);
                }
                using (var p = new Pen(enabled ? (isDomeNextHover ? Color.White : Color.FromArgb(220, 220, 220)) : Color.FromArgb(90, 90, 90), 1.8f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    float cx = btnDomeNext.Width / 2f;
                    float cy = btnDomeNext.Height / 2f;
                    g.DrawLine(p, cx - 3f, cy - 5f, cx + 2f, cy);
                    g.DrawLine(p, cx + 2f, cy, cx - 3f, cy + 5f);
                }
            };
            btnDomeNext.Click += (s, e) => {
                if (IsCornerDockMode)
                {
                    ScrollCornerCards(1);
                    return;
                }
                int step = (pnlViewport != null ? Math.Max(180, pnlViewport.ClientSize.Width - 20) : 240);
                SetScrollX(scrollX + step);
            };
            toolTip.SetToolTip(btnDomeNext, "Следующие карточки");
            this.Controls.Add(btnDomeNext);

            pnlCornerEmpty = new CornerEmptyCardPanel
            {
                Visible = false,
                Size = new Size(250, 112)
            };
            this.Controls.Add(pnlCornerEmpty);

            this.MouseWheel += OnViewportMouseWheel;

            // Внешняя тонкая рамка 1px вокруг формы (полупрозрачная стеклянная грань Win11)
            this.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                if (IsCornerDockMode)
                {
                    int w = this.Width;
                    int h = this.Height;
                    float scale = Math.Max(0.85f, Math.Min(1.2f, (float)w / 440f));
                    // Рисуем тонкие разделительные дуги строго посередине между ярусами
                    using (var guidePen = new Pen(Color.FromArgb(16, 255, 255, 255), 1f))
                    {
                        guidePen.DashStyle = DashStyle.Dot;
                        DrawCornerArc(e.Graphics, guidePen, w, h, dockPositionMode, 74f * scale);
                        DrawCornerArc(e.Graphics, guidePen, w, h, dockPositionMode, 121f * scale);
                        DrawCornerArc(e.Graphics, guidePen, w, h, dockPositionMode, 171f * scale);
                        DrawCornerArc(e.Graphics, guidePen, w, h, dockPositionMode, 222f * scale);
                        DrawCornerArc(e.Graphics, guidePen, w, h, dockPositionMode, 403f * scale);
                    }
                    using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255), 1.2f))
                    {
                        DrawCornerArc(e.Graphics, pen, w, h, dockPositionMode, (float)w - 0.5f);
                    }
                }
                else
                {
                    using (var pen = new Pen(Color.FromArgb(65, 255, 255, 255), 1.2f))
                    {
                        e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
                    }
                }
            };

            this.ResumeLayout(false);
            Logger.Log("BuildUI: finished successfully");
            }
            catch (Exception ex)
            {
                Logger.Log("BuildUI EXCEPTION: " + ex.ToString());
                throw;
            }
        }

        private void UpdateDragModeButtonVisual()
        {
            if (btnDragMode == null) return;
            btnDragMode.Text = "";
            bool isCopy = string.Equals(Config.SuperHubDragMode, "Copy", StringComparison.OrdinalIgnoreCase);
            if (isCopy)
            {
                toolTip.SetToolTip(btnDragMode, "Режим Drag-and-Drop: КОПИРОВАНИЕ файлов.\nПри перетаскивании наружу исходные файлы остаются на месте.\nКликните для переключения в режим Переноса.");
            }
            else if (string.Equals(Config.SuperHubDragMode, "Link", StringComparison.OrdinalIgnoreCase))
            {
                toolTip.SetToolTip(btnDragMode, "Режим Drag-and-Drop: ЯРЛЫК.\nПри перетаскивании создаётся ярлык (.lnk) на исходный файл.\nКликните для переключения в режим Копирования.");
            }
            else
            {
                toolTip.SetToolTip(btnDragMode, "Режим Drag-and-Drop: ПЕРЕМЕЩЕНИЕ файлов.\nПри перетаскивании наружу файлы переносятся (вырезаются).\nКликните для переключения в режим Ярлыка.");
            }
        }

        private void OnViewportMouseWheel(object sender, MouseEventArgs e)
        {
            if (IsCornerDockMode)
            {
                int delta = (e.Delta > 0) ? -1 : 1;
                ScrollCornerCards(delta);
                return;
            }
            if (IsHorizontalDockMode)
            {
                if (maxScrollX <= 0) return;
                int step = 60;
                int delta = (e.Delta > 0) ? -step : step;
                SetScrollX(scrollX + delta);
            }
            else
            {
                if (maxScrollY <= 0) return;
                int step = 50;
                int delta = (e.Delta > 0) ? -step : step;
                SetScrollY(scrollY + delta);
            }
        }

        private void SetScrollX(int newX)
        {
            if (newX < 0) newX = 0;
            if (newX > maxScrollX) newX = maxScrollX;
            if (newX != scrollX)
            {
                scrollX = newX;
                if (cardsContainer != null)
                {
                    int padLeft = IsCornerDockMode ? 4 : 12;
                    cardsContainer.Left = padLeft - scrollX;
                    cardsContainer.Invalidate(true);
                }
                if (pnlViewport != null) pnlViewport.Invalidate();
            }
        }

        private void SetScrollY(int newY)
        {
            if (newY < 0) newY = 0;
            if (newY > maxScrollY) newY = maxScrollY;
            if (newY != scrollY)
            {
                scrollY = newY;
                if (cardsContainer != null)
                {
                    cardsContainer.Top = -scrollY;
                    cardsContainer.Invalidate(true);
                }
                if (pnlViewport != null) pnlViewport.Invalidate();
            }
        }

        private Rectangle GetScrollbarThumbRect()
        {
            if (IsHorizontalDockMode)
            {
                if (maxScrollX <= 0) return Rectangle.Empty;

                int trackLeft = 14;
                int trackRight = pnlViewport.Width - 14;
                int trackWidth = trackRight - trackLeft;
                if (trackWidth <= 20) return Rectangle.Empty;

                int totalContentWidth = Math.Max(1, cardsContainer != null ? cardsContainer.Width : pnlViewport.Width);
                int thumbWidth = Math.Max(28, (int)((float)pnlViewport.Width / totalContentWidth * trackWidth));
                int availableMove = trackWidth - thumbWidth;

                int thumbX = trackLeft + (int)((float)scrollX / maxScrollX * availableMove);
                int thumbH = isHoveringScroll || isDraggingScroll ? 6 : 4;
                int thumbY = pnlViewport.Height - thumbH - 2;

                return new Rectangle(thumbX, thumbY, thumbWidth, thumbH);
            }
            else
            {
                if (maxScrollY <= 0) return Rectangle.Empty;

                int trackTop = 6;
                int trackBottom = pnlViewport.Height - 14;
                int trackHeight = trackBottom - trackTop;
                if (trackHeight <= 20) return Rectangle.Empty;

                int totalContentHeight = Math.Max(1, cardsContainer != null ? cardsContainer.Height : pnlViewport.Height);
                int thumbHeight = Math.Max(28, (int)((float)pnlViewport.Height / totalContentHeight * trackHeight));
                int availableMove = trackHeight - thumbHeight;

                int thumbY = trackTop + (int)((float)scrollY / maxScrollY * availableMove);
                int thumbW = isHoveringScroll || isDraggingScroll ? 6 : 4;
                int thumbX = pnlViewport.Width - thumbW - 3;

                return new Rectangle(thumbX, thumbY, thumbW, thumbHeight);
            }
        }

        private void RenderFluentScrollbar(object sender, PaintEventArgs e)
        {
            if (IsHorizontalDockMode ? (maxScrollX <= 0) : (maxScrollY <= 0)) return;

            Rectangle thumb = GetScrollbarThumbRect();
            if (thumb.IsEmpty) return;

            Color thumbColor = isDraggingScroll
                ? ThemeHelper.ScrollBarThumbActive
                : (isHoveringScroll ? ThemeHelper.ScrollBarThumbHover : ThemeHelper.ScrollBarThumb);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(thumbColor))
            using (var path = CreateRoundedPath(thumb, 2))
            {
                e.Graphics.FillPath(brush, path);
            }
        }

        private void OnScrollbarMouseMove(object sender, MouseEventArgs e)
        {
            if (IsHorizontalDockMode)
            {
                if (maxScrollX <= 0) return;

                if (isDraggingScroll)
                {
                    int trackLeft = 14;
                    int trackWidth = (pnlViewport.Width - 14) - trackLeft;
                    Rectangle thumb = GetScrollbarThumbRect();
                    int availableMove = Math.Max(1, trackWidth - thumb.Width);

                    int deltaMouse = e.X - dragStartMouseX;
                    int deltaScroll = (int)((float)deltaMouse / availableMove * maxScrollX);
                    SetScrollX(dragStartScrollX + deltaScroll);
                    return;
                }

                Rectangle scrollArea = new Rectangle(0, pnlViewport.Height - 12, pnlViewport.Width, 12);
                bool hover = scrollArea.Contains(e.Location);
                if (hover != isHoveringScroll)
                {
                    isHoveringScroll = hover;
                    pnlViewport.Invalidate();
                }
            }
            else
            {
                if (maxScrollY <= 0) return;

                if (isDraggingScroll)
                {
                    int trackHeight = pnlViewport.Height - 20;
                    Rectangle thumb = GetScrollbarThumbRect();
                    int availableMove = Math.Max(1, trackHeight - thumb.Height);

                    int deltaMouse = e.Y - dragStartMouseY;
                    int deltaScroll = (int)((float)deltaMouse / availableMove * maxScrollY);
                    SetScrollY(dragStartScrollY + deltaScroll);
                    return;
                }

                Rectangle scrollArea = new Rectangle(pnlViewport.Width - 12, 0, 12, pnlViewport.Height);
                bool hover = scrollArea.Contains(e.Location);
                if (hover != isHoveringScroll)
                {
                    isHoveringScroll = hover;
                    pnlViewport.Invalidate();
                }
            }
        }

        private void OnScrollbarMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            if (IsHorizontalDockMode)
            {
                if (maxScrollX <= 0) return;

                Rectangle thumb = GetScrollbarThumbRect();
                if (thumb.Contains(e.Location))
                {
                    isDraggingScroll = true;
                    dragStartMouseX = e.X;
                    dragStartScrollX = scrollX;
                }
                else if (e.Y >= pnlViewport.Height - 12)
                {
                    if (e.X < thumb.X) SetScrollX(scrollX - pnlViewport.Width / 2);
                    else SetScrollX(scrollX + pnlViewport.Width / 2);
                }
            }
            else
            {
                if (maxScrollY <= 0) return;

                Rectangle thumb = GetScrollbarThumbRect();
                if (thumb.Contains(e.Location))
                {
                    isDraggingScroll = true;
                    dragStartMouseY = e.Y;
                    dragStartScrollY = scrollY;
                }
                else if (e.X >= pnlViewport.Width - 12)
                {
                    if (e.Y < thumb.Y) SetScrollY(scrollY - pnlViewport.Height / 2);
                    else SetScrollY(scrollY + pnlViewport.Height / 2);
                }
            }
        }

        private void OnScrollbarMouseUp(object sender, MouseEventArgs e)
        {
            if (isDraggingScroll)
            {
                isDraggingScroll = false;
                pnlViewport.Invalidate();
            }
        }

        private void OpenNewNoteDialog()
        {
            try
            {
                isShowingModalDialog = true;
                using (var form = new NewNoteForm())
                {
                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        string note = form.NoteText;
                        if (!string.IsNullOrWhiteSpace(note))
                        {
                            ClipboardHistoryManager.Instance.AddCustomSuperHubItem(null, note);
                            SwitchMainTab(1);
                            if (cardsContainerSuperHub != null)
                            {
                                PopulateContainer(cardsContainerSuperHub, true);
                                UpdateContainerScroll();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("OpenNewNoteDialog error: " + ex.Message);
            }
            finally
            {
                isShowingModalDialog = false;
            }
        }

        public void RegisterSuperHubDropTarget(Control c)
        {
            if (c == null) return;
            try
            {
                c.AllowDrop = true;
                c.DragEnter -= OnFormDragEnter;
                c.DragEnter += OnFormDragEnter;
                c.DragOver -= OnFormDragOver;
                c.DragOver += OnFormDragOver;
                c.DragDrop -= OnFormDragDrop;
                c.DragDrop += OnFormDragDrop;

                foreach (Control child in c.Controls)
                {
                    RegisterSuperHubDropTarget(child);
                }
            }
            catch {}
        }

        private void OnFormDragEnter(object sender, DragEventArgs e)
        {
            if (isDraggingItemOut || IsOurInternalDrag(e.Data))
            {
                e.Effect = DragDropEffects.None;
                return;
            }
            if (ImageDropHelper.HasDropData(e.Data))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void OnFormDragOver(object sender, DragEventArgs e)
        {
            if (isDraggingItemOut || IsOurInternalDrag(e.Data))
            {
                e.Effect = DragDropEffects.None;
                return;
            }
            if (ImageDropHelper.HasDropData(e.Data))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void OnFormDragDrop(object sender, DragEventArgs e)
        {
            if (isDraggingItemOut || IsOurInternalDrag(e.Data))
            {
                e.Effect = DragDropEffects.None;
                return;
            }
            try
            {
                bool added = ClipboardHistoryManager.Instance.AddFromDataObject(e.Data, true);
                if (added)
                {
                    SwitchMainTab(1);
                    if (cardsContainerSuperHub != null)
                    {
                        PopulateContainer(cardsContainerSuperHub, true);
                        UpdateContainerScroll();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("OnFormDragDrop error: " + ex.Message);
            }
        }

        public void SwitchMainTab(int mainTab)
        {
            currentMainTab = Math.Max(0, Math.Min(1, mainTab));
            Config.LastActiveFlyoutTab = currentMainTab;
            UpdateTabsVisual();

            bool isHub = (currentMainTab == 1);
            if (cardsContainerClipboard != null) cardsContainerClipboard.Visible = !isHub;
            if (cardsContainerSuperHub != null) cardsContainerSuperHub.Visible = isHub;
            cardsContainer = isHub ? cardsContainerSuperHub : cardsContainerClipboard;

            if (isHub)
            {
                RebuildFolderTabs();
            }

            LayoutControls();

            if (isHub)
            {
                if (superHubNeedsRefresh && cardsContainerSuperHub != null)
                {
                    PopulateContainer(cardsContainerSuperHub, true);
                    superHubNeedsRefresh = false;
                }
                else
                {
                    ApplyFilterToContainer(cardsContainerSuperHub, true);
                }
            }
            else
            {
                if (clipboardNeedsRefresh && cardsContainerClipboard != null)
                {
                    PopulateContainer(cardsContainerClipboard, false);
                    clipboardNeedsRefresh = false;
                }
                else
                {
                    ApplyFilterToContainer(cardsContainerClipboard, false);
                }
            }
            UpdateContainerScroll();
        }

        public void SwitchFilter(int filterIndex)
        {
            currentFilterIndex = filterIndex;
            scrollX = 0;
            scrollY = 0;
            UpdateTabsVisual();
            ApplyFilterToAllContainers();
        }

        private void ApplyFilterToAllContainers()
        {
            ApplyFilterToContainer(cardsContainerClipboard, false);
            ApplyFilterToContainer(cardsContainerSuperHub, true);
        }

        private static bool IsImageFilePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = SafeGetExtension(path).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp" || ext == ".webp";
        }

        private void ScrollCornerCards(int delta)
        {
            Panel container = (currentMainTab == 1) ? cardsContainerSuperHub : cardsContainerClipboard;
            if (container == null) return;

            int totalMatching = 0;
            string search = (txtSearch != null) ? txtSearch.Text.Trim() : null;

            foreach (Control c in container.Controls)
            {
                ClipboardItem item = c.Tag as ClipboardItem;
                if (item == null) continue;

                bool matches = true;
                if (currentFilterIndex == 1)
                {
                    if (item.Type != ClipboardItemType.Text || ClipboardHistoryManager.IsUrl(item.TextContent)) matches = false;
                }
                else if (currentFilterIndex == 2)
                {
                    bool isImg = (item.Type == ClipboardItemType.Image);
                    if (!isImg && item.Type == ClipboardItemType.Files && item.FilePaths != null)
                    {
                        foreach (var f in item.FilePaths)
                        {
                            if (IsImageFilePath(f)) { isImg = true; break; }
                        }
                    }
                    if (!isImg) matches = false;
                }
                else if (currentFilterIndex == 3)
                {
                    if (item.Type != ClipboardItemType.Text || !ClipboardHistoryManager.IsUrl(item.TextContent)) matches = false;
                }
                else if (currentFilterIndex == 4)
                {
                    bool hasDir = false;
                    if (item.Type == ClipboardItemType.Files && item.FilePaths != null)
                    {
                        foreach (var f in item.FilePaths)
                        {
                            if (SafeDirectoryExists(f)) { hasDir = true; break; }
                        }
                    }
                    if (!hasDir) matches = false;
                }
                else if (currentFilterIndex == 5)
                {
                    bool hasFile = false;
                    if (item.Type == ClipboardItemType.Files && item.FilePaths != null)
                    {
                        foreach (var f in item.FilePaths)
                        {
                            if (!string.IsNullOrEmpty(f) && !SafeDirectoryExists(f)) { hasFile = true; break; }
                        }
                    }
                    if (!hasFile) matches = false;
                }
                else if (currentFilterIndex == 6)
                {
                    if (!item.IsPinned) matches = false;
                }

                if (matches && !string.IsNullOrEmpty(search))
                {
                    matches = false;
                    if (item.TextContent != null && item.TextContent.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) matches = true;
                    else if (item.SourceApp != null && item.SourceApp.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) matches = true;
                    else if (item.ImagePath != null && item.ImagePath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) matches = true;
                    else if (item.FilePaths != null)
                    {
                        foreach (var f in item.FilePaths)
                        {
                            if (f != null && f.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) { matches = true; break; }
                        }
                    }
                }

                if (matches) totalMatching++;
            }

            int maxIndex = Math.Max(0, totalMatching - 2);
            int newIdx = arcScrollIndex + delta;
            if (newIdx < 0) newIdx = 0;
            if (newIdx > maxIndex) newIdx = maxIndex;

            if (newIdx != arcScrollIndex)
            {
                arcScrollIndex = newIdx;
                LayoutCornerCards(container, currentMainTab == 1);
            }
        }

        private void LayoutCornerCards(Panel container, bool isHub)
        {
            if (container == null) return;
            container.SuspendLayout();

            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;

            PointF origin;
            float startAngle, sweepAngle, bisectorAngle;
            GetCornerGeometry(w, h, dockPositionMode, out origin, out startAngle, out sweepAngle, out bisectorAngle);

            string search = (txtSearch != null) ? txtSearch.Text.Trim() : null;

            Label emptyFilterLbl = null;
            var matchingCards = new List<Control>();

            foreach (Control c in container.Controls)
            {
                if (c is Label && string.Equals(c.Tag as string, "empty_filter_label", StringComparison.Ordinal))
                {
                    emptyFilterLbl = (Label)c;
                    continue;
                }

                ClipboardItem item = c.Tag as ClipboardItem;
                if (item == null) continue;

                bool matches = true;
                if (currentFilterIndex == 1) // Текст
                {
                    if (item.Type != ClipboardItemType.Text || ClipboardHistoryManager.IsUrl(item.TextContent)) matches = false;
                }
                else if (currentFilterIndex == 2) // Снимки
                {
                    bool isImg = (item.Type == ClipboardItemType.Image);
                    if (!isImg && item.Type == ClipboardItemType.Files && item.FilePaths != null)
                    {
                        foreach (var f in item.FilePaths)
                        {
                            if (IsImageFilePath(f)) { isImg = true; break; }
                        }
                    }
                    if (!isImg) matches = false;
                }
                else if (currentFilterIndex == 3) // Ссылки
                {
                    if (item.Type != ClipboardItemType.Text || !ClipboardHistoryManager.IsUrl(item.TextContent)) matches = false;
                }
                else if (currentFilterIndex == 4) // Папки
                {
                    bool hasDir = false;
                    if (item.Type == ClipboardItemType.Files && item.FilePaths != null)
                    {
                        foreach (var f in item.FilePaths)
                        {
                            if (!string.IsNullOrEmpty(f) && string.IsNullOrEmpty(SafeGetExtension(f)) && SafeDirectoryExists(f)) { hasDir = true; break; }
                        }
                    }
                    if (!hasDir) matches = false;
                }
                else if (currentFilterIndex == 5) // Файлы
                {
                    bool hasFile = false;
                    if (item.Type == ClipboardItemType.Files && item.FilePaths != null)
                    {
                        foreach (var f in item.FilePaths)
                        {
                            if (!string.IsNullOrEmpty(f))
                            {
                                if (!string.IsNullOrEmpty(SafeGetExtension(f)) || !SafeDirectoryExists(f)) { hasFile = true; break; }
                            }
                        }
                    }
                    if (!hasFile) matches = false;
                }
                else if (currentFilterIndex == 6) // Закрепленные
                {
                    if (!item.IsPinned) matches = false;
                }

                if (matches && !string.IsNullOrEmpty(search))
                {
                    matches = false;
                    if (item.TextContent != null && item.TextContent.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) matches = true;
                    else if (item.SourceApp != null && item.SourceApp.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) matches = true;
                    else if (item.ImagePath != null && item.ImagePath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) matches = true;
                    else if (item.FilePaths != null)
                    {
                        foreach (var f in item.FilePaths)
                        {
                            if (f != null && f.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) { matches = true; break; }
                        }
                    }
                }

                if (matches)
                {
                    matchingCards.Add(c);
                }
                else
                {
                    c.Visible = false;
                }
            }

            int cardW = 154;
            int cardH = 104;
            float scale = Math.Max(0.85f, Math.Min(1.2f, (float)w / 440f));
            float rCards = 312f * scale;

            if (matchingCards.Count == 0)
            {
                if (emptyFilterLbl != null) emptyFilterLbl.Visible = false;

                if (pnlCornerEmpty != null)
                {
                    pnlCornerEmpty.IsHubMode = isHub;
                    if (!string.IsNullOrEmpty(search))
                    {
                        pnlCornerEmpty.MainTitle = "Ничего не найдено";
                        pnlCornerEmpty.SubTitle = "Попробуйте изменить поисковый запрос";
                    }
                    else if (currentFilterIndex != 0)
                    {
                        pnlCornerEmpty.MainTitle = "В этой категории пусто";
                        pnlCornerEmpty.SubTitle = "Нет подходящих элементов в фильтре";
                    }
                    else if (isHub)
                    {
                        pnlCornerEmpty.MainTitle = "Полка SuperHub пуста";
                        pnlCornerEmpty.SubTitle = "Перетащите сюда файлы или нажмите Ctrl+Alt+C";
                    }
                    else
                    {
                        pnlCornerEmpty.MainTitle = "Буфер обмена пуст";
                        pnlCornerEmpty.SubTitle = "Скопируйте текст или файл (Ctrl+C)";
                    }

                    int emptyW = 250;
                    int emptyH = 112;
                    PointF ptEmpty = GetPolarPoint(origin, rCards, bisectorAngle);
                    pnlCornerEmpty.Size = new Size(emptyW, emptyH);
                    pnlCornerEmpty.Location = new Point((int)(ptEmpty.X - emptyW / 2), (int)(ptEmpty.Y - emptyH / 2));
                    pnlCornerEmpty.Visible = true;
                    pnlCornerEmpty.BringToFront();
                    pnlCornerEmpty.Invalidate();
                }

                if (btnDomePrev != null) btnDomePrev.Visible = false;
                if (btnDomeNext != null) btnDomeNext.Visible = false;
            }
            else
            {
                if (emptyFilterLbl != null) emptyFilterLbl.Visible = false;
                if (pnlCornerEmpty != null) pnlCornerEmpty.Visible = false;

                int total = matchingCards.Count;
                int visibleSlots = 2;
                int maxIdx = Math.Max(0, total - visibleSlots);
                if (arcScrollIndex > maxIdx) arcScrollIndex = maxIdx;
                if (arcScrollIndex < 0) arcScrollIndex = 0;

                for (int i = 0; i < total; i++)
                {
                    Control c = matchingCards[i];
                    if (total == 1)
                    {
                        PointF pt = GetPolarPoint(origin, rCards, bisectorAngle);
                        c.Size = new Size(cardW, cardH);
                        c.Location = new Point((int)(pt.X - cardW / 2), (int)(pt.Y - cardH / 2));
                        LayoutCardInternalControls(c, cardW, cardH, true);
                        c.Visible = true;
                        c.BringToFront();
                    }
                    else
                    {
                        if (i >= arcScrollIndex && i < arcScrollIndex + visibleSlots)
                        {
                            int slot = i - arcScrollIndex; // 0 или 1
                            float angle = (slot == 0) ? (bisectorAngle - 18f) : (bisectorAngle + 18f);
                            PointF pt = GetPolarPoint(origin, rCards, angle);
                            c.Size = new Size(cardW, cardH);
                            c.Location = new Point((int)(pt.X - cardW / 2), (int)(pt.Y - cardH / 2));
                            LayoutCardInternalControls(c, cardW, cardH, true);
                            c.Visible = true;
                            c.BringToFront();
                        }
                        else
                        {
                            c.Visible = false;
                        }
                    }
                }

                if (total > visibleSlots)
                {
                    float rNav = Math.Min((float)w - 20f, rCards + 96f);
                    if (btnDomePrev != null)
                    {
                        PointF ptPrev = GetPolarPoint(origin, rNav, bisectorAngle - 4.5f);
                        btnDomePrev.Size = new Size(28, 28);
                        btnDomePrev.Location = new Point((int)(ptPrev.X - 14), (int)(ptPrev.Y - 14));
                        btnDomePrev.Enabled = (arcScrollIndex > 0);
                        btnDomePrev.Visible = true;
                        btnDomePrev.BringToFront();
                        btnDomePrev.Invalidate();
                    }
                    if (btnDomeNext != null)
                    {
                        PointF ptNext = GetPolarPoint(origin, rNav, bisectorAngle + 4.5f);
                        btnDomeNext.Size = new Size(28, 28);
                        btnDomeNext.Location = new Point((int)(ptNext.X - 14), (int)(ptNext.Y - 14));
                        btnDomeNext.Enabled = (arcScrollIndex < maxIdx);
                        btnDomeNext.Visible = true;
                        btnDomeNext.BringToFront();
                        btnDomeNext.Invalidate();
                    }
                }
                else
                {
                    if (btnDomePrev != null) btnDomePrev.Visible = false;
                    if (btnDomeNext != null) btnDomeNext.Visible = false;
                }
            }

            container.ResumeLayout(true);
        }

        private void ApplyFilterToContainer(Panel container, bool isHub)
        {
            if (container == null) return;
            if (this.IsCornerDockMode)
            {
                LayoutCornerCards(container, isHub);
                return;
            }
            container.SuspendLayout();

            string search = (txtSearch != null) ? txtSearch.Text.Trim() : null;
            bool isHoriz = this.IsHorizontalDockMode;

            int cardY = 4;
            int cardX = 4;
            int availableH = Math.Max(70, pnlViewport != null ? (pnlViewport.ClientSize.Height - 14) : 90);
            int cardW;
            if (this.IsCornerDockMode)
            {
                cardW = pnlViewport != null ? Math.Max(180, (pnlViewport.ClientSize.Width - 14) / 2) : 236;
            }
            else if (isHoriz)
            {
                cardW = 210;
            }
            else
            {
                cardW = pnlViewport != null ? Math.Max(100, pnlViewport.ClientSize.Width - 28) : container.Width;
            }
            int visibleCount = 0;
            int totalItemsCount = 0;
            Label emptyFilterLbl = null;

            foreach (Control c in container.Controls)
            {
                if (c is Label && string.Equals(c.Tag as string, "empty_filter_label", StringComparison.Ordinal))
                {
                    emptyFilterLbl = (Label)c;
                    continue;
                }

                ClipboardItem item = c.Tag as ClipboardItem;
                if (item == null) continue;
                totalItemsCount++;

                bool matches = true;
                if (currentFilterIndex == 1) // Текст
                {
                    if (item.Type != ClipboardItemType.Text || ClipboardHistoryManager.IsUrl(item.TextContent)) matches = false;
                }
                else if (currentFilterIndex == 2) // Снимки
                {
                    bool isImg = (item.Type == ClipboardItemType.Image);
                    if (!isImg && item.Type == ClipboardItemType.Files && item.FilePaths != null)
                    {
                        foreach (var f in item.FilePaths)
                        {
                            if (IsImageFilePath(f)) { isImg = true; break; }
                        }
                    }
                    if (!isImg) matches = false;
                }
                else if (currentFilterIndex == 3) // Ссылки
                {
                    if (item.Type != ClipboardItemType.Text || !ClipboardHistoryManager.IsUrl(item.TextContent)) matches = false;
                }
                else if (currentFilterIndex == 4) // Папки
                {
                    bool hasDir = false;
                    if (item.Type == ClipboardItemType.Files && item.FilePaths != null)
                    {
                        foreach (var f in item.FilePaths)
                        {
                            if (SafeDirectoryExists(f)) { hasDir = true; break; }
                        }
                    }
                    if (!hasDir) matches = false;
                }
                else if (currentFilterIndex == 5) // Файлы (обычные файлы, не папки)
                {
                    bool hasFile = false;
                    if (item.Type == ClipboardItemType.Files && item.FilePaths != null)
                    {
                        foreach (var f in item.FilePaths)
                        {
                            if (!string.IsNullOrEmpty(f) && !SafeDirectoryExists(f)) { hasFile = true; break; }
                        }
                    }
                    if (!hasFile) matches = false;
                }
                else if (currentFilterIndex == 6) // Закрепленные
                {
                    if (!item.IsPinned) matches = false;
                }

                if (matches && !string.IsNullOrEmpty(search))
                {
                    matches = false;
                    if (item.TextContent != null && item.TextContent.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) matches = true;
                    else if (item.SourceApp != null && item.SourceApp.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) matches = true;
                    else if (item.ImagePath != null && item.ImagePath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) matches = true;
                    else if (item.FilePaths != null)
                    {
                        foreach (var f in item.FilePaths)
                        {
                            if (f != null && f.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) { matches = true; break; }
                        }
                    }
                }

                c.Visible = matches;
                if (matches)
                {
                    if (isHoriz)
                    {
                        c.Location = new Point(cardX, 4);
                        LayoutCardInternalControls(c, cardW, availableH, true);
                        cardX += cardW + 8;
                    }
                    else
                    {
                        c.Location = new Point(0, cardY);
                        LayoutCardInternalControls(c, cardW, c.Height, false);
                        cardY += c.Height + 8;
                    }
                    c.Invalidate();
                    visibleCount++;
                }
            }

            if (emptyFilterLbl != null)
            {
                if (visibleCount == 0)
                {
                    if (totalItemsCount == 0)
                    {
                        if (isHub)
                        {
                            string folder = Config.SuperHubFoldersEnabled ? Config.SuperHubCurrentFolder : null;
                            emptyFilterLbl.Text = (!string.IsNullOrEmpty(folder) && !string.Equals(folder, "Все", StringComparison.OrdinalIgnoreCase))
                                ? ("Папка \"" + folder + "\" пуста.\nПеретащите сюда файлы или переместите из других папок через ПКМ.")
                                : "Полка SuperHub пуста.\nПеретащите сюда файлы или нажмите Ctrl+Alt+C.";
                        }
                        else
                        {
                            emptyFilterLbl.Text = "Здесь ничего нет.\nСкопированные элементы появятся в этом окне.";
                        }
                    }
                    else
                    {
                        emptyFilterLbl.Text = "В этой категории пока нет элементов.\nНажмите «Все», чтобы показать все элементы.";
                    }

                    if (isHoriz)
                    {
                        emptyFilterLbl.Width = Math.Max(320, pnlViewport != null ? (pnlViewport.ClientSize.Width - 40) : 320);
                        emptyFilterLbl.Height = Math.Max(60, availableH);
                        emptyFilterLbl.Location = new Point(20, 10);
                        cardX = emptyFilterLbl.Right + 20;
                    }
                    else
                    {
                        emptyFilterLbl.Width = cardW;
                        emptyFilterLbl.Location = new Point(0, 30);
                        cardY = 200;
                    }
                    emptyFilterLbl.Visible = true;
                }
                else
                {
                    emptyFilterLbl.Visible = false;
                }
            }

            if (isHoriz)
            {
                container.Top = 0;
                container.Height = pnlViewport != null ? pnlViewport.ClientSize.Height : (availableH + 14);
                container.Width = Math.Max(cardX + 16, pnlViewport != null ? pnlViewport.ClientSize.Width : 200);
            }
            else
            {
                container.Height = Math.Max(cardY + 10, 100);
                container.Width = cardW;
            }

            container.ResumeLayout(true);
            UpdateContainerScroll();
        }

        private void RebuildFolderTabs()
        {
            if (flpFolders == null) return;
            flpFolders.SuspendLayout();
            for (int i = flpFolders.Controls.Count - 1; i >= 0; i--)
            {
                Control c = flpFolders.Controls[i];
                flpFolders.Controls.RemoveAt(i);
                c.Dispose();
            }
            folderButtons.Clear();

            string raw = Config.SuperHubFolders;
            if (string.IsNullOrEmpty(raw)) raw = "Все|Общее|Работа|Проекты";
            string[] folders = raw.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            string curFolder = Config.SuperHubCurrentFolder;
            if (string.IsNullOrEmpty(curFolder)) curFolder = "Все";

            foreach (var fName in folders)
            {
                string f = fName.Trim();
                if (f.Length == 0) continue;
                string folderTag = f;
                bool isAll = string.Equals(folderTag, "Все", StringComparison.OrdinalIgnoreCase);

                Button btnF = new Button
                {
                    Height = 26,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                    TabStop = false,
                    Margin = new Padding(2, 0, 2, 0),
                    Tag = folderTag
                };
                btnF.FlatAppearance.BorderSize = 0;
                btnF.FlatAppearance.MouseOverBackColor = Color.Transparent;
                btnF.FlatAppearance.MouseDownBackColor = Color.Transparent;

                bool act = string.Equals(curFolder, folderTag, StringComparison.OrdinalIgnoreCase);
                using (var testFont = new Font("Segoe UI", 8.8f, act ? FontStyle.Bold : FontStyle.Regular))
                {
                    Size textSize = TextRenderer.MeasureText(folderTag, testFont);
                    btnF.Width = isAll ? Math.Max(42, textSize.Width + 20) : Math.Max(56, textSize.Width + 28);
                }

                bool isHover = false;
                btnF.MouseEnter += (s, e) => { isHover = true; btnF.Invalidate(); };
                btnF.MouseLeave += (s, e) => { isHover = false; btnF.Invalidate(); };
                btnF.Paint += (s, e) => {
                    Graphics g = e.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;

                    bool isCurrent = string.Equals(Config.SuperHubCurrentFolder, folderTag, StringComparison.OrdinalIgnoreCase);
                    Rectangle r = new Rectangle(0, 0, btnF.Width - 1, btnF.Height - 1);
                    using (var path = CreateRoundedPath(r, 5))
                    {
                        Color bg = isCurrent
                            ? (ThemeHelper.IsDarkTheme() ? Color.FromArgb(44, 255, 255, 255) : Color.FromArgb(232, 240, 252))
                            : (isHover ? Color.FromArgb(28, 255, 255, 255) : Color.Transparent);

                        using (var brush = new SolidBrush(bg))
                        {
                            g.FillPath(brush, path);
                        }
                        if (isCurrent)
                        {
                            using (var pen = new Pen(Color.FromArgb(85, 255, 255, 255), 1f))
                            {
                                g.DrawPath(pen, path);
                            }
                        }
                        else if (isHover)
                        {
                            using (var pen = new Pen(Color.FromArgb(45, 255, 255, 255), 1f))
                            {
                                g.DrawPath(pen, path);
                            }
                        }
                    }

                    int textLeft = 6;
                    if (!isAll)
                    {
                        Color iconCol = isCurrent
                            ? ThemeHelper.SuperHubSecondaryAccent
                            : (isHover ? Color.FromArgb(235, 195, 110) : Color.FromArgb(170, 150, 100));
                        Rectangle iconRect = new Rectangle(7, (btnF.Height - 11) / 2, 13, 11);
                        DrawFolderIcon(g, iconRect, iconCol);
                        textLeft = 23;
                    }

                    Color txtColor = isCurrent
                        ? ThemeHelper.TextPrimary
                        : (isHover ? ThemeHelper.TextPrimary : ThemeHelper.TextSecondary);

                    using (var fFont = new Font("Segoe UI", 8.8f, isCurrent ? FontStyle.Bold : FontStyle.Regular))
                    using (var brush = new SolidBrush(txtColor))
                    {
                        var sf = new StringFormat
                        {
                            Alignment = (textLeft > 6) ? StringAlignment.Near : StringAlignment.Center,
                            LineAlignment = StringAlignment.Center,
                            FormatFlags = StringFormatFlags.NoWrap
                        };
                        RectangleF textRect = new RectangleF(textLeft, 0, btnF.Width - textLeft - 4, btnF.Height);
                        g.DrawString(folderTag, fFont, brush, textRect, sf);
                    }
                };

                btnF.Click += (s, e) => {
                    Config.SuperHubCurrentFolder = folderTag;
                    flpFolders.Invalidate(true);
                    if (cardsContainerSuperHub != null)
                    {
                        PopulateContainer(cardsContainerSuperHub, true);
                        UpdateContainerScroll();
                    }
                };
                btnF.MouseWheel += OnFoldersMouseWheel;

                if (!isAll)
                {
                    btnF.DoubleClick += (s, e) => PromptRenameFolder(folderTag);

                    btnF.MouseDown += (s, e) => {
                        if (e.Button == MouseButtons.Right)
                        {
                            ContextMenu cm = new ContextMenu();
                            cm.MenuItems.Add(new MenuItem("✏️ Переименовать папку", (ts, te) => PromptRenameFolder(folderTag)));
                            cm.MenuItems.Add(new MenuItem("🗑️ Удалить папку", (ts, te) => DeleteFolder(folderTag)));
                            cm.Show(btnF, e.Location);
                        }
                    };
                }

                folderButtons.Add(btnF);
                flpFolders.Controls.Add(btnF);
            }

            // Кнопка добавления новой папки [+]
            btnAddFolder = new Button
            {
                Size = new Size(24, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabStop = false,
                Margin = new Padding(3, 1, 3, 1)
            };
            btnAddFolder.FlatAppearance.BorderSize = 0;
            btnAddFolder.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnAddFolder.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool isAddHover = false;
            btnAddFolder.MouseEnter += (s, e) => { isAddHover = true; btnAddFolder.Invalidate(); };
            btnAddFolder.MouseLeave += (s, e) => { isAddHover = false; btnAddFolder.Invalidate(); };
            btnAddFolder.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(0, 0, btnAddFolder.Width - 1, btnAddFolder.Height - 1);
                if (isAddHover)
                {
                    using (var path = CreateRoundedPath(r, 12))
                    using (var b = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                    {
                        g.FillPath(b, path);
                    }
                }
                using (var p = new Pen(isAddHover ? ThemeHelper.Accent : ThemeHelper.TextSecondary, 1.4f))
                {
                    int cx = btnAddFolder.Width / 2;
                    int cy = btnAddFolder.Height / 2;
                    g.DrawLine(p, cx - 4, cy, cx + 4, cy);
                    g.DrawLine(p, cx, cy - 4, cx, cy + 4);
                }
            };
            btnAddFolder.Click += (s, e) => PromptCreateFolder();
            btnAddFolder.MouseWheel += OnFoldersMouseWheel;
            toolTip.SetToolTip(btnAddFolder, "Создать новую папку SuperHub");
            flpFolders.Controls.Add(btnAddFolder);

            flpFolders.ResumeLayout(true);
            int totalFolderW = 0;
            foreach (Control c in flpFolders.Controls)
            {
                if (c.Visible) totalFolderW = Math.Max(totalFolderW, c.Right);
            }
            if (pnlFolders != null)
            {
                flpFolders.Width = Math.Max(pnlFolders.Width - 16, totalFolderW + 20);
            }
        }

        private void PromptCreateFolder()
        {
            try
            {
                isShowingModalDialog = true;
                using (var dlg = new FolderPromptForm("Новая папка", "Введите название папки:", ""))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        string name = dlg.InputValue;
                        if (!string.IsNullOrEmpty(name))
                        {
                            name = name.Replace("|", "").Trim();
                            string cur = Config.SuperHubFolders;
                            if (string.IsNullOrEmpty(cur)) cur = "Все|Общее";
                            string[] existing = cur.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                            bool alreadyExists = false;
                            foreach (var ex in existing)
                            {
                                if (string.Equals(ex.Trim(), name, StringComparison.OrdinalIgnoreCase))
                                {
                                    alreadyExists = true;
                                    break;
                                }
                            }
                            if (!alreadyExists)
                            {
                                Config.SuperHubFolders = cur + "|" + name;
                            }
                            Config.SuperHubCurrentFolder = name;
                            RebuildFolderTabs();
                            if (cardsContainerSuperHub != null)
                            {
                                PopulateContainer(cardsContainerSuperHub, true);
                                UpdateContainerScroll();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("PromptCreateFolder error: " + ex.Message);
            }
            finally
            {
                isShowingModalDialog = false;
            }
        }

        private void PromptRenameFolder(string oldName)
        {
            if (string.Equals(oldName, "Все", StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                isShowingModalDialog = true;
                using (var dlg = new FolderPromptForm("Переименовать папку", "Новое название папки:", oldName))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        string newName = dlg.InputValue;
                        if (!string.IsNullOrEmpty(newName) && !string.Equals(newName, oldName, StringComparison.OrdinalIgnoreCase))
                        {
                            newName = newName.Replace("|", "").Trim();
                            string[] parts = Config.SuperHubFolders.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                            var newParts = new List<string>();
                            foreach (var p in parts)
                            {
                                if (string.Equals(p.Trim(), oldName, StringComparison.OrdinalIgnoreCase)) newParts.Add(newName);
                                else newParts.Add(p.Trim());
                            }
                            Config.SuperHubFolders = string.Join("|", newParts.ToArray());

                            // Переносим элементы из старой папки в новую
                            var allHubItems = ClipboardHistoryManager.Instance.GetItems(null, true);
                            foreach (var it in allHubItems)
                            {
                                if (string.Equals(it.Folder, oldName, StringComparison.OrdinalIgnoreCase))
                                {
                                    ClipboardHistoryManager.Instance.SetItemFolder(it.Id, newName);
                                }
                            }

                            if (string.Equals(Config.SuperHubCurrentFolder, oldName, StringComparison.OrdinalIgnoreCase))
                            {
                                Config.SuperHubCurrentFolder = newName;
                            }
                            RebuildFolderTabs();
                            if (cardsContainerSuperHub != null)
                            {
                                PopulateContainer(cardsContainerSuperHub, true);
                                UpdateContainerScroll();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("PromptRenameFolder error: " + ex.Message);
            }
            finally
            {
                isShowingModalDialog = false;
            }
        }

        private void DeleteFolder(string folderName)
        {
            if (string.Equals(folderName, "Все", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(folderName, "Общее", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Эту папку нельзя удалить.", "SuperHub", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(string.Format("Удалить папку \"{0}\"?\nЭлементы из нее будут перемещены в папку \"Общее\".", folderName),
                "Удаление папки", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            string[] parts = Config.SuperHubFolders.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            var newParts = new List<string>();
            foreach (var p in parts)
            {
                if (!string.Equals(p.Trim(), folderName, StringComparison.OrdinalIgnoreCase))
                {
                    newParts.Add(p.Trim());
                }
            }
            Config.SuperHubFolders = string.Join("|", newParts.ToArray());

            // Перемещаем элементы в "Общее"
            var allHubItems = ClipboardHistoryManager.Instance.GetItems(null, true);
            foreach (var it in allHubItems)
            {
                if (string.Equals(it.Folder, folderName, StringComparison.OrdinalIgnoreCase))
                {
                    ClipboardHistoryManager.Instance.SetItemFolder(it.Id, "Общее");
                }
            }

            if (string.Equals(Config.SuperHubCurrentFolder, folderName, StringComparison.OrdinalIgnoreCase))
            {
                Config.SuperHubCurrentFolder = "Все";
            }

            RebuildFolderTabs();
            if (cardsContainerSuperHub != null)
            {
                PopulateContainer(cardsContainerSuperHub, true);
                UpdateContainerScroll();
            }
        }

        private static void DrawPasteActionIcon(Graphics g, Rectangle r, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            using (var pen = new Pen(color, 1.4f))
            {
                // Планшет / лоток
                g.DrawLine(pen, cx - 6f, cy - 1f, cx - 6f, cy + 6f);
                g.DrawLine(pen, cx - 6f, cy + 6f, cx + 6f, cy + 6f);
                g.DrawLine(pen, cx + 6f, cy + 6f, cx + 6f, cy - 1f);
                // Стрелочка вниз (вставка)
                g.DrawLine(pen, cx, cy - 6f, cx, cy + 2f);
                g.DrawLine(pen, cx - 3.5f, cy - 1.5f, cx, cy + 2f);
                g.DrawLine(pen, cx + 3.5f, cy - 1.5f, cx, cy + 2f);
            }
        }

        private void UpdateTabsVisual()
        {
            if (pnlMainTabs != null) pnlMainTabs.Invalidate();

            if (btnMainClipboard != null) btnMainClipboard.Invalidate();
            if (btnMainSuperHub != null) btnMainSuperHub.Invalidate();

            for (int i = 0; i < filterButtons.Count; i++)
            {
                filterButtons[i].BackColor = Color.Transparent;
                filterButtons[i].Invalidate();
            }
            if (flpSubTabs != null) flpSubTabs.Invalidate();

            if (btnClickAction != null) btnClickAction.Invalidate();
            if (btnDragMode != null) btnDragMode.Invalidate();
            UpdateSubBarLayout();
        }

        private static void SafeDisposeControls(Control parent)
        {
            if (parent == null) return;
            for (int i = parent.Controls.Count - 1; i >= 0; i--)
            {
                try
                {
                    Control c = parent.Controls[i];
                    parent.Controls.RemoveAt(i);
                    var pic = c as PictureBox;
                    if (pic != null && pic.Image != null)
                    {
                        try { pic.Image.Dispose(); pic.Image = null; } catch {}
                    }
                    c.Dispose();
                }
                catch {}
            }
        }

        private void PopulateContainer(Panel container, bool isHub)
        {
            if (container == null) return;
            container.SuspendLayout();
            SafeDisposeControls(container);

            string folder = (isHub && Config.SuperHubFoldersEnabled) ? Config.SuperHubCurrentFolder : null;
            var list = isHub
                ? ClipboardHistoryManager.Instance.GetItems(null, true, null, folder, false, false, false)
                : ClipboardHistoryManager.Instance.GetItems(null, false, null, null, false, false, false);

            int cardY = 4;
            int cardW = container.Width;

            Label emptyFilterLbl = new Label
            {
                Tag = "empty_filter_label",
                Text = isHub
                    ? "Полка SuperHub пуста.\nПеретащите сюда файлы или нажмите Ctrl+Alt+C."
                    : "Здесь ничего нет.\nСкопированные элементы появятся в этом окне.",
                ForeColor = ThemeHelper.TextMuted,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9.5f),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(cardW, 140),
                Location = new Point(0, 30),
                Visible = (list.Count == 0)
            };
            container.Controls.Add(emptyFilterLbl);

            foreach (var item in list)
            {
                try
                {
                    var card = CreateItemCard(item, cardW, isHub);
                    if (card != null)
                    {
                        card.Tag = item;
                        card.Location = new Point(0, cardY);
                        container.Controls.Add(card);
                        if (isHub)
                        {
                            RegisterSuperHubDropTarget(card);
                        }
                        cardY += card.Height + 8;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log("PopulateContainer item error: " + ex.ToString());
                }
            }

            container.ResumeLayout(true);
            ApplyFilterToContainer(container, isHub);
        }

        public void RefreshItems()
        {
            superHubNeedsRefresh = true;
            clipboardNeedsRefresh = true;
            if (currentMainTab == 0)
            {
                if (cardsContainerClipboard != null)
                {
                    PopulateContainer(cardsContainerClipboard, false);
                    clipboardNeedsRefresh = false;
                }
            }
            else
            {
                if (cardsContainerSuperHub != null)
                {
                    PopulateContainer(cardsContainerSuperHub, true);
                    superHubNeedsRefresh = false;
                }
            }
            UpdateContainerScroll();
            UpdateSubBarLayout();
        }

        private void PreloadInactiveTab()
        {
            if (this.IsDisposed) return;
            if (currentMainTab == 1)
            {
                if (clipboardNeedsRefresh && cardsContainerClipboard != null)
                {
                    PopulateContainer(cardsContainerClipboard, false);
                    clipboardNeedsRefresh = false;
                }
            }
            else
            {
                if (superHubNeedsRefresh && cardsContainerSuperHub != null)
                {
                    PopulateContainer(cardsContainerSuperHub, true);
                    superHubNeedsRefresh = false;
                }
            }
        }

        private void UpdateContainerScroll()
        {
            if (cardsContainer == null || pnlViewport == null) return;
            if (IsHorizontalDockMode)
            {
                int extraScroll = 0;
                int padLeft = IsCornerDockMode ? 4 : 12;
                maxScrollX = Math.Max(0, cardsContainer.Width - pnlViewport.ClientSize.Width + extraScroll);
                if (scrollX > maxScrollX) scrollX = maxScrollX;
                if (scrollX < 0) scrollX = 0;
                cardsContainer.Left = padLeft - scrollX;
                cardsContainer.Top = 0;
            }
            else
            {
                maxScrollY = Math.Max(0, cardsContainer.Height - pnlViewport.Height);
                if (scrollY > maxScrollY) scrollY = maxScrollY;
                if (scrollY < 0) scrollY = 0;
                cardsContainer.Top = -scrollY;
                cardsContainer.Left = 14;
            }
            pnlViewport.Invalidate();
        }

        private Control CreateItemCard(ClipboardItem item, int width, bool isHub)
        {
            string firstFile = (item.FilePaths != null && item.FilePaths.Count > 0) ? item.FilePaths[0] : null;
            bool isPdf = false;
            bool isImageFile = false;
            if (item.Type == ClipboardItemType.Files && !string.IsNullOrEmpty(firstFile))
            {
                string ext = SafeGetExtension(firstFile).ToLowerInvariant();
                if (ext == ".pdf")
                {
                    isPdf = true;
                }
                else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp" || ext == ".webp")
                {
                    if (SafeFileExists(firstFile))
                    {
                        isImageFile = true;
                    }
                }
            }

            bool isImageCard = (item.Type == ClipboardItemType.Image && SafeFileExists(item.ImagePath)) || isImageFile;
            string imgSourcePath = (item.Type == ClipboardItemType.Image) ? item.ImagePath : firstFile;

            // Определяем высоту карточки 1-в-1 как в Windows 11
            int cardH = 74;
            if (isImageCard)
            {
                cardH = 124;
            }
            else if (item.Type == ClipboardItemType.Text)
            {
                string rawCheck = (item.TextContent ?? "").Trim();
                bool hasNewlines = rawCheck.IndexOf('\n') >= 0;
                cardH = (rawCheck.Length > 42 || hasNewlines) ? 80 : 66;
            }
            else
            {
                cardH = 68;
            }

            AcrylicCardPanel card = new AcrylicCardPanel
            {
                Size = new Size(width, cardH),
                BackColor = ThemeHelper.CardBackground,
                Cursor = Cursors.Hand
            };

            bool isCardHovered = false;
            card.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                bool isFirstVisible = false;
                if (card.Parent != null)
                {
                    foreach (Control sib in card.Parent.Controls)
                    {
                        if (sib.Visible && sib.Tag is ClipboardItem)
                        {
                            isFirstVisible = (sib == card);
                            break;
                        }
                    }
                }

                Color bColor;
                float bWidth = 1f;
                if (isFirstVisible)
                {
                    bColor = ThemeHelper.IsDarkTheme() ? Color.FromArgb(115, 255, 255, 255) : ThemeHelper.Accent;
                    bWidth = 1.1f;
                }
                else if (item.IsPinned)
                {
                    bColor = isCardHovered ? ThemeHelper.Accent : Color.FromArgb(80, 255, 255, 255);
                    bWidth = 1f;
                }
                else
                {
                    bColor = ThemeHelper.IsDarkTheme()
                        ? Color.FromArgb(isCardHovered ? 75 : 34, 255, 255, 255)
                        : (isCardHovered ? ThemeHelper.CardBorderHover : ThemeHelper.CardBorder);
                }

                using (var pen = new Pen(bColor, bWidth))
                using (var path = CreateRoundedPath(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 6))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            };

            // Определение пути для открытия/запуска по двойному клику
            string targetPathForLaunch = null;
            if (item.Type == ClipboardItemType.Image && SafeFileExists(item.ImagePath))
            {
                targetPathForLaunch = item.ImagePath;
            }
            else if (item.Type == ClipboardItemType.Files && item.FilePaths != null && item.FilePaths.Count > 0)
            {
                targetPathForLaunch = item.FilePaths[0];
            }
            else if (item.Type == ClipboardItemType.Text && !string.IsNullOrEmpty(item.TextContent))
            {
                string trimmed = item.TextContent.Trim();
                if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                    (trimmed.Length < 260 && (trimmed.IndexOf('\\') >= 0 || trimmed.IndexOf('/') >= 0) && (SafeFileExists(trimmed) || SafeDirectoryExists(trimmed))))
                {
                    targetPathForLaunch = trimmed;
                }
            }

            string cardTip = isHub
                ? "Клик: скопировать в буфер\nДвойной клик: открыть\nЗажать и потянуть: перетащить наружу\nПравый клик или [⋯]: меню действий"
                : (string.Equals(Config.ClipboardClickAction, "Paste", StringComparison.OrdinalIgnoreCase)
                    ? "Клик: вставить в активное окно [Ctrl+V]\nДвойной клик: открыть\nПравый клик или [⋯]: меню действий"
                    : "Клик: скопировать в буфер\nДвойной клик: открыть\nПравый клик или [⋯]: меню действий");

            if (isImageCard && !string.IsNullOrEmpty(imgSourcePath))
            {
                // Широкое превью изображения внутри карточки (как в нативном буфере Windows 11)
                int previewW = Math.Max(80, width - 50);
                int previewH = cardH - 20;
                Image wideThumb = ThumbnailCache.GetThumbnail(imgSourcePath, 260, 104);

                Panel pnlImgPreview = new DoubleBufferedPanel
                {
                    Location = new Point(12, 10),
                    Size = new Size(previewW, previewH),
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                    Tag = "img_preview"
                };

                Image finalWideThumb = wideThumb;
                pnlImgPreview.Paint += (s, e) => {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    Rectangle rImg = new Rectangle(0, 0, pnlImgPreview.Width - 1, pnlImgPreview.Height - 1);
                    using (var path = CreateRoundedPath(rImg, 4))
                    {
                        using (var bgBrush = new SolidBrush(Color.FromArgb(24, 24, 24)))
                        {
                            e.Graphics.FillPath(bgBrush, path);
                        }
                        if (finalWideThumb != null)
                        {
                            e.Graphics.SetClip(path);
                            e.Graphics.DrawImage(finalWideThumb, 0, 0, pnlImgPreview.Width, pnlImgPreview.Height);
                            e.Graphics.ResetClip();
                        }
                        using (var borderPen = new Pen(Color.FromArgb(35, 255, 255, 255), 1f))
                        {
                            e.Graphics.DrawPath(borderPen, path);
                        }
                    }
                };
                toolTip.SetToolTip(pnlImgPreview, cardTip);
                card.Controls.Add(pnlImgPreview);
            }
            else if (item.Type == ClipboardItemType.Text)
            {
                // В нативном буфере Windows 11 текстовые элементы и ссылки отображаются чистым белым текстом слева без иконки
                string rawText = (item.TextContent ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Trim();
                if (isHub && rawText.StartsWith("Заметка в SuperHub:", StringComparison.OrdinalIgnoreCase))
                {
                    rawText = rawText.Substring(rawText.IndexOf(':') + 1).Trim();
                }
                if (rawText.Length > 260) rawText = rawText.Substring(0, 260) + "...";

                int textLeft = 14;
                int textWidth = Math.Max(40, width - textLeft - 38);

                Label lblText = new Label
                {
                    Text = rawText,
                    Font = new Font("Segoe UI", 9.25f, FontStyle.Regular),
                    ForeColor = ThemeHelper.TextPrimary,
                    BackColor = Color.Transparent,
                    Location = new Point(textLeft, 11),
                    Size = new Size(textWidth, cardH - 22),
                    AutoEllipsis = true,
                    Cursor = Cursors.Hand
                };
                toolTip.SetToolTip(lblText, cardTip);
                card.Controls.Add(lblText);
            }
            else
            {
                // Карточка файла или папки (не изображение)
                Image fileIcon = null;
                if (!isPdf && !string.IsNullOrEmpty(firstFile))
                {
                    try { fileIcon = FileIconHelper.GetIconForPath(firstFile); } catch {}
                }

                Panel pnlFileIcon = new DoubleBufferedPanel
                {
                    Location = new Point(12, 14),
                    Size = new Size(40, 40),
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };

                Image finalFileIcon = fileIcon;
                bool finalIsPdf = isPdf;
                pnlFileIcon.Paint += (s, e) => {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    if (finalIsPdf)
                    {
                        DrawPdfDocThumbnail(e.Graphics, new Rectangle(0, 0, 40, 40));
                    }
                    else
                    {
                        using (var path = CreateRoundedPath(new Rectangle(0, 0, 39, 39), 5))
                        using (var bgBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                        using (var borderPen = new Pen(Color.FromArgb(55, 255, 255, 255), 1f))
                        {
                            e.Graphics.FillPath(bgBrush, path);
                            e.Graphics.DrawPath(borderPen, path);
                        }
                        if (finalFileIcon != null)
                        {
                            int ix = (40 - finalFileIcon.Width) / 2;
                            int iy = (40 - finalFileIcon.Height) / 2;
                            e.Graphics.DrawImage(finalFileIcon, ix, iy);
                        }
                    }
                };
                toolTip.SetToolTip(pnlFileIcon, cardTip);
                card.Controls.Add(pnlFileIcon);

                string titleText = "Файлы";
                if (!string.IsNullOrEmpty(firstFile))
                {
                    string fn = SafeGetFileName(firstFile);
                    titleText = (item.FilePaths != null && item.FilePaths.Count > 1)
                        ? (fn + " (+" + (item.FilePaths.Count - 1) + ")")
                        : fn;
                }

                string fType = isPdf ? "PDF" : (string.IsNullOrEmpty(SafeGetExtension(firstFile)) && SafeDirectoryExists(firstFile) ? "Папка" : "Файл");
                string sz = item.FileSizeBytes > 0 ? (" • " + (item.FileSizeBytes / 1024) + " КБ") : "";
                string subText = fType + sz + " • " + item.Timestamp.ToString("HH:mm");

                int textLeft = 60;
                int textWidth = Math.Max(40, width - textLeft - 38);

                Label lblTitle = new Label
                {
                    Text = titleText,
                    Font = CardTitleFont,
                    ForeColor = ThemeHelper.TextPrimary,
                    BackColor = Color.Transparent,
                    Location = new Point(textLeft, 13),
                    Size = new Size(textWidth, 20),
                    AutoEllipsis = true,
                    Cursor = Cursors.Hand
                };
                Label lblSub = new Label
                {
                    Text = subText,
                    Font = CardSubFont,
                    ForeColor = ThemeHelper.TextSecondary,
                    BackColor = Color.Transparent,
                    Location = new Point(textLeft, 34),
                    Size = new Size(textWidth, 18),
                    AutoEllipsis = true,
                    Cursor = Cursors.Hand
                };
                toolTip.SetToolTip(lblTitle, cardTip);
                toolTip.SetToolTip(lblSub, cardTip);
                card.Controls.Add(lblTitle);
                card.Controls.Add(lblSub);
            }

            // Правая колонка из 2 кнопок (1-в-1 как в Windows 11: вверху [⋯], внизу наклонная булавка [📌])
            Button btnMore = new Button
            {
                Location = new Point(card.Width - 30, 6),
                Size = new Size(24, 22),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabStop = false,
                Tag = "btn_more"
            };
            btnMore.FlatAppearance.BorderSize = 0;
            btnMore.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnMore.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool isHoverMore = false;
            btnMore.MouseEnter += (s, e) => { isHoverMore = true; btnMore.Invalidate(); };
            btnMore.MouseLeave += (s, e) => { isHoverMore = false; btnMore.Invalidate(); };
            btnMore.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (isHoverMore)
                {
                    using (var b = new SolidBrush(Color.FromArgb(50, 255, 255, 255)))
                    using (var p = CreateRoundedPath(new Rectangle(0, 0, btnMore.Width - 1, btnMore.Height - 1), 5))
                    {
                        g.FillPath(b, p);
                    }
                }
                DrawMoreDotsIcon(g, new Rectangle(0, 0, btnMore.Width, btnMore.Height), isHoverMore ? ThemeHelper.TextPrimary : ThemeHelper.TextSecondary);
            };
            btnMore.Click += (s, e) => {
                ContextMenu cm = CreateCardContextMenu(item);
                cm.Show(btnMore, new Point(0, btnMore.Height));
            };
            toolTip.SetToolTip(btnMore, "Дополнительные действия (просмотр, удаление, в SuperHub)");
            card.Controls.Add(btnMore);

            Button btnPin = new Button
            {
                Location = new Point(card.Width - 30, cardH - 28),
                Size = new Size(24, 22),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TabStop = false,
                Tag = "btn_pin"
            };
            btnPin.FlatAppearance.BorderSize = 0;
            btnPin.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnPin.FlatAppearance.MouseDownBackColor = Color.Transparent;
            bool isHoverPin = false;
            btnPin.MouseEnter += (s, e) => { isHoverPin = true; btnPin.Invalidate(); };
            btnPin.MouseLeave += (s, e) => { isHoverPin = false; btnPin.Invalidate(); };
            btnPin.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (isHoverPin)
                {
                    using (var b = new SolidBrush(Color.FromArgb(50, 255, 255, 255)))
                    using (var p = CreateRoundedPath(new Rectangle(0, 0, btnPin.Width - 1, btnPin.Height - 1), 5))
                    {
                        g.FillPath(b, p);
                    }
                }
                Color pinColor = item.IsPinned ? ThemeHelper.Accent : (isHoverPin ? ThemeHelper.TextPrimary : ThemeHelper.TextSecondary);
                DrawWin11PinIcon(g, new Rectangle(0, 0, btnPin.Width, btnPin.Height), pinColor, item.IsPinned);
            };
            btnPin.Click += (s, e) => {
                ClipboardHistoryManager.Instance.TogglePin(item.Id);
                RefreshItems();
            };
            toolTip.SetToolTip(btnPin, item.IsPinned ? "Открепить элемент" : "Закрепить элемент");
            card.Controls.Add(btnPin);

            // Обработка кликов, наведения и перетаскивания (Drag-and-Drop)
            Point dragStartScreenPt = Point.Empty;
            Control dragInitiator = null;
            bool isPotentialDrag = false;
            bool wasActuallyDragged = false;
            System.Windows.Forms.Timer cardClickTimer = null;
            bool isCardDoubleClickOccurred = false;

            Action executeCardSingleClick = () => {
                if (this.IsDisposed || !this.Visible) return;
                if (isHub)
                {
                    CopyItem(item);
                    ShowCopiedNotification();
                }
                else
                {
                    bool isPaste = string.Equals(Config.ClipboardClickAction, "Paste", StringComparison.OrdinalIgnoreCase);
                    if (isPaste)
                    {
                        PasteItem(item);
                    }
                    else
                    {
                        CopyItem(item);
                        ShowCopiedNotification();
                        if (!isWindowPinned)
                        {
                            CloseFlyout();
                        }
                    }
                }
            };

            Action<Control> attachEvents = null;
            attachEvents = (c) => {
                if (c is Button) return;

                c.MouseEnter += (s, e) => {
                    isCardHovered = true;
                    card.IsCardHovered = true;
                    card.Invalidate(true);
                };
                c.MouseLeave += (s, e) => {
                    isCardHovered = false;
                    card.IsCardHovered = false;
                    card.Invalidate(true);
                };

                c.MouseDown += (s, e) => {
                    if (c is Button) return;
                    if (e.Button == MouseButtons.Left)
                    {
                        if (e.Clicks >= 2)
                        {
                            isCardDoubleClickOccurred = true;
                            if (cardClickTimer != null)
                            {
                                cardClickTimer.Stop();
                                cardClickTimer.Dispose();
                                cardClickTimer = null;
                            }
                            if (!string.IsNullOrEmpty(targetPathForLaunch))
                            {
                                OpenItemInAssociatedApp(targetPathForLaunch);
                            }
                            return;
                        }
                        else if (e.Clicks == 1)
                        {
                            isCardDoubleClickOccurred = false;
                            wasActuallyDragged = false;
                            dragStartScreenPt = Cursor.Position;
                            dragInitiator = c;
                            isPotentialDrag = true;
                        }
                    }
                };

                c.MouseClick += (s, e) => {
                    if (c is Button) return;
                    if (wasActuallyDragged)
                    {
                        wasActuallyDragged = false;
                        return;
                    }
                    if (e.Button == MouseButtons.Right)
                    {
                        if (cardClickTimer != null) { cardClickTimer.Stop(); }
                        ContextMenu cm = CreateCardContextMenu(item);
                        cm.Show(c, e.Location);
                        return;
                    }

                    if (e.Button == MouseButtons.Left && !isCardDoubleClickOccurred)
                    {
                        bool instantClipboardPaste = !isHub && string.Equals(Config.ClipboardClickAction, "Paste", StringComparison.OrdinalIgnoreCase);
                        if (!instantClipboardPaste && !string.IsNullOrEmpty(targetPathForLaunch))
                        {
                            if (cardClickTimer != null)
                            {
                                cardClickTimer.Stop();
                                cardClickTimer.Dispose();
                                cardClickTimer = null;
                            }
                            cardClickTimer = new System.Windows.Forms.Timer();
                            cardClickTimer.Interval = Math.Min(220, SystemInformation.DoubleClickTime);
                            cardClickTimer.Tick += (st, se) => {
                                if (cardClickTimer != null)
                                {
                                    cardClickTimer.Stop();
                                    cardClickTimer.Dispose();
                                    cardClickTimer = null;
                                }
                                if (!isCardDoubleClickOccurred && !wasActuallyDragged)
                                {
                                    executeCardSingleClick();
                                }
                            };
                            cardClickTimer.Start();
                        }
                        else
                        {
                            executeCardSingleClick();
                        }
                    }
                };

                c.DoubleClick += (s, e) => {
                    if (!(c is Button) && !string.IsNullOrEmpty(targetPathForLaunch))
                    {
                        isCardDoubleClickOccurred = true;
                        if (cardClickTimer != null)
                        {
                            cardClickTimer.Stop();
                            cardClickTimer.Dispose();
                            cardClickTimer = null;
                        }
                        OpenItemInAssociatedApp(targetPathForLaunch);
                    }
                };

                c.MouseMove += (s, e) => {
                    if (isPotentialDrag && (Control.MouseButtons & MouseButtons.Left) == MouseButtons.Left && !(c is Button))
                    {
                        Point curPos = Cursor.Position;
                        int dx = Math.Abs(curPos.X - dragStartScreenPt.X);
                        int dy = Math.Abs(curPos.Y - dragStartScreenPt.Y);
                        if (dx >= SystemInformation.DragSize.Width || dy >= SystemInformation.DragSize.Height)
                        {
                            isPotentialDrag = false;
                            wasActuallyDragged = true;
                            if (cardClickTimer != null) { cardClickTimer.Stop(); }
                            StartDragItem(item, dragInitiator ?? card);
                        }
                    }
                };

                c.MouseUp += (s, e) => {
                    isPotentialDrag = false;
                };

                foreach (Control sub in c.Controls)
                {
                    if (!(sub is Button))
                    {
                        attachEvents(sub);
                    }
                }
            };
            attachEvents(card);
            toolTip.SetToolTip(card, cardTip);

            return card;
        }

        private void HandleCardClick(ClipboardItem item)
        {
            bool isPaste = string.Equals(Config.ClipboardClickAction, "Paste", StringComparison.OrdinalIgnoreCase);
            if (isPaste && currentMainTab == 0)
            {
                PasteItem(item);
            }
            else
            {
                CopyItem(item);
                ShowCopiedNotification();
                if (!isWindowPinned)
                {
                    CloseFlyout();
                }
            }
        }

        private void ShowCopiedNotification()
        {
            try
            {
                toolTip.Show("✓ Скопировано в буфер обмена!", this, this.Width / 2 - 80, this.Height - 60, 1500);
            }
            catch {}
        }

        private void OpenItemInAssociatedApp(string path)
        {
            try
            {
                if (SafeFileExists(path) || SafeDirectoryExists(path))
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось открыть файл:\n" + ex.Message, "Буфер обмена", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private ContextMenu CreateCardContextMenu(ClipboardItem item)
        {
            var menu = new ContextMenu();

            menu.MenuItems.Add(new MenuItem("👁 Просмотр (зум/текст)", (s, e) => ItemViewerForm.ShowViewer(item)));

            string targetPath = null;
            if (item.Type == ClipboardItemType.Image) targetPath = item.ImagePath;
            else if (item.Type == ClipboardItemType.Files && item.FilePaths != null && item.FilePaths.Count > 0) targetPath = item.FilePaths[0];

            if (!string.IsNullOrEmpty(targetPath) && (SafeFileExists(targetPath) || SafeDirectoryExists(targetPath)))
            {
                menu.MenuItems.Add(new MenuItem("▷ Открыть в программе", (s, e) => OpenItemInAssociatedApp(targetPath)));
            }

            menu.MenuItems.Add(new MenuItem("-"));
            menu.MenuItems.Add(new MenuItem("Вставить в активное окно", (s, e) => PasteItem(item)));
            menu.MenuItems.Add(new MenuItem("Скопировать в буфер", (s, e) => {
                CopyItem(item);
                ShowCopiedNotification();
            }));
            menu.MenuItems.Add(new MenuItem(item.IsPinned ? "Открепить" : "Закрепить вверху", (s, e) => {
                ClipboardHistoryManager.Instance.TogglePin(item.Id);
                RefreshItems();
            }));
            menu.MenuItems.Add(new MenuItem(item.IsInSuperHub ? "Убрать из SuperHub" : "Добавить в SuperHub", (s, e) => {
                ClipboardHistoryManager.Instance.ToggleSuperHub(item.Id);
                RefreshItems();
            }));

            if (item.IsInSuperHub && Config.SuperHubFoldersEnabled)
            {
                var folderMenu = new MenuItem("📁 Переместить в папку");
                string raw = Config.SuperHubFolders;
                if (string.IsNullOrEmpty(raw)) raw = "Все|Общее|Работа|Проекты";
                string[] folders = raw.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                string curItFolder = string.IsNullOrEmpty(item.Folder) ? "Общее" : item.Folder;
                foreach (var f in folders)
                {
                    string targetF = f.Trim();
                    if (string.Equals(targetF, "Все", StringComparison.OrdinalIgnoreCase)) continue;
                    bool isCur = string.Equals(curItFolder, targetF, StringComparison.OrdinalIgnoreCase);
                    string title = isCur ? ("✓ " + targetF) : ("   " + targetF);
                    folderMenu.MenuItems.Add(new MenuItem(title, (s, e) => {
                        ClipboardHistoryManager.Instance.SetItemFolder(item.Id, targetF);
                        RefreshItems();
                    }) { Enabled = !isCur });
                }
                menu.MenuItems.Add(folderMenu);
            }

            if (item.Type == ClipboardItemType.Image || item.Type == ClipboardItemType.Files)
            {
                menu.MenuItems.Add(new MenuItem("-"));
                menu.MenuItems.Add(new MenuItem("Сохранить на Рабочий стол", (s, e) => SaveItemToDesktop(item)));
                menu.MenuItems.Add(new MenuItem("📁 Показать в Проводнике", (s, e) => ShowItemInExplorer(item)));
            }

            menu.MenuItems.Add(new MenuItem("-"));
            menu.MenuItems.Add(new MenuItem("🗑️ Удалить", (s, e) => {
                this.Activate();
                ClipboardHistoryManager.Instance.DeleteItem(item.Id);
                RefreshItems();
            }));
            return menu;
        }

        private void CopyItem(ClipboardItem item)
        {
            try
            {
                if (item.Type == ClipboardItemType.Image && SafeFileExists(item.ImagePath))
                {
                    ClipboardListenerWindow.AugmentClipboardWithImage(item.ImagePath);
                }
                else if (item.Type == ClipboardItemType.Text)
                {
                    Clipboard.SetText(item.TextContent ?? "");
                }
                else if (item.Type == ClipboardItemType.Files && item.FilePaths != null && item.FilePaths.Count > 0)
                {
                    var sc = new StringCollection();
                    foreach (var f in item.FilePaths) if (SafeFileExists(f) || SafeDirectoryExists(f)) sc.Add(f);
                    if (sc.Count > 0) Clipboard.SetFileDropList(sc);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("CopyItem error: " + ex.Message);
            }
        }

        private void PasteItem(ClipboardItem item)
        {
            CopyItem(item);

            IntPtr targetWnd = previousForegroundWindow;
            if (targetWnd == IntPtr.Zero || IsOurAppWindow(targetWnd) || Program.IsTaskbarOrTrayWindow(targetWnd))
            {
                if (Program.LastKnownUserWindow != IntPtr.Zero && !IsOurAppWindow(Program.LastKnownUserWindow) && !Program.IsTaskbarOrTrayWindow(Program.LastKnownUserWindow))
                {
                    targetWnd = Program.LastKnownUserWindow;
                }
                else
                {
                    Screen scr = Screen.FromControl(this);
                    targetWnd = Program.FindLastActiveUserWindow(scr);
                }
            }

            if (targetWnd != IntPtr.Zero && !IsOurAppWindow(targetWnd))
            {
                this.previousForegroundWindow = targetWnd;
            }

            bool wasPinned = isWindowPinned;
            if (!wasPinned)
            {
                CloseFlyout();
            }

            ThreadPool.QueueUserWorkItem(_ => {
                try
                {
                    // Короткая пауза для закрытия окна (если не закреплено) и активации целевого окна
                    Thread.Sleep(wasPinned ? 25 : 45);
                    if (targetWnd != IntPtr.Zero)
                    {
                        Program.ForceForegroundWindow(targetWnd);
                        Thread.Sleep(35);
                    }

                    // Сброс модификаторов (Alt, Shift, Win), если они остались зажатыми
                    keybd_event(0x12, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // Alt
                    keybd_event(0x10, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // Shift
                    keybd_event(0x5B, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // Win

                    // Отправка комбинации Ctrl+V
                    keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(15);
                    keybd_event(VK_V, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(25);
                    keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    Thread.Sleep(15);
                    keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                    Logger.Log(string.Format("Pasted item {0} into targetWnd={1}", item.Id, targetWnd));
                }
                catch (Exception ex)
                {
                    Logger.Log("PasteItem simulated input error: " + ex.Message);
                }
            });
        }

        private void StartDragItem(ClipboardItem item, Control sourceControl)
        {
            List<string> tempLnkFiles = null;
            try
            {
                DataObject data = new DataObject();
                data.SetData("PasteImageAsFile_InternalDrag", "true");
                data.SetData("PasteImageAsFile_SourceItemId", item.Id ?? "");
                bool isMove = string.Equals(Config.SuperHubDragMode, "Move", StringComparison.OrdinalIgnoreCase);
                bool isLink = string.Equals(Config.SuperHubDragMode, "Link", StringComparison.OrdinalIgnoreCase);

                // Собираем исходные пути файлов
                var sourcePaths = new List<string>();
                if (item.Type == ClipboardItemType.Files && item.FilePaths != null && item.FilePaths.Count > 0)
                {
                    foreach (var f in item.FilePaths)
                    {
                        if (SafeFileExists(f) || SafeDirectoryExists(f)) sourcePaths.Add(f);
                    }
                }
                else if (item.Type == ClipboardItemType.Image && SafeFileExists(item.ImagePath))
                {
                    sourcePaths.Add(item.ImagePath);
                    try
                    {
                        using (var fs = new FileStream(item.ImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var img = Image.FromStream(fs))
                        {
                            data.SetImage(new Bitmap(img));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("StartDragItem SetImage error: " + ex.Message);
                    }
                }
                else if (item.Type == ClipboardItemType.Text && !string.IsNullOrEmpty(item.TextContent))
                {
                    string txt = item.TextContent.Trim();
                    if (SafeFileExists(txt) || SafeDirectoryExists(txt))
                    {
                        sourcePaths.Add(txt);
                    }
                    else
                    {
                        // Чистый текст: создаем временный .txt файл, чтобы его можно было
                        // вытянуть на Рабочий стол или в любую папку Проводника как текстовый файл!
                        string tempTxt = CreateTempTextFile(item.TextContent);
                        if (!string.IsNullOrEmpty(tempTxt))
                        {
                            sourcePaths.Add(tempTxt);
                            if (tempLnkFiles == null) tempLnkFiles = new List<string>();
                            tempLnkFiles.Add(tempTxt);
                        }
                    }
                }

                if (isLink && sourcePaths.Count > 0)
                {
                    // Режим Ярлык: создаём .lnk файлы во временной папке и перетаскиваем их
                    tempLnkFiles = CreateTempShortcuts(sourcePaths);
                    if (tempLnkFiles.Count > 0)
                    {
                        var sc = new StringCollection();
                        foreach (var lnk in tempLnkFiles) sc.Add(lnk);
                        data.SetFileDropList(sc);
                    }
                }
                else if (sourcePaths.Count > 0)
                {
                    var sc = new StringCollection();
                    foreach (var f in sourcePaths) sc.Add(f);
                    data.SetFileDropList(sc);
                }

                if (item.Type == ClipboardItemType.Text)
                {
                    data.SetText(item.TextContent ?? "");
                }

                DragDropEffects allowed = DragDropEffects.Copy;
                if (isMove)
                {
                    allowed = DragDropEffects.Move | DragDropEffects.Copy;
                }
                else if (isLink)
                {
                    allowed = DragDropEffects.Link | DragDropEffects.Copy;
                }

                IsGlobalDraggingActive = true;
                isDraggingItemOut = true;
                GlobalDraggingItemId = item.Id ?? "";
                GlobalDraggingPaths.Clear();
                foreach (var p in sourcePaths) GlobalDraggingPaths.Add(p);

                try
                {
                    Control initiator = sourceControl ?? this;
                    initiator.DoDragDrop(data, allowed);
                }
                finally
                {
                    isDraggingItemOut = false;
                    IsGlobalDraggingActive = false;
                    GlobalDragEndTimeTicks = DateTime.UtcNow.Ticks;

                    Rectangle b = this.Bounds;
                    b.Inflate(36, 36);
                    Point cur = Cursor.Position;
                    if (b.Contains(cur))
                    {
                        this.Activate();
                    }
                    else if (!isWindowPinned)
                    {
                        // Дроп произошел вне окна (в целевую папку или приложение).
                        // Мягко закрываем SuperHub через короткую паузу (250 мс), если не зажата ЛКМ.
                        var closeTimer = new System.Windows.Forms.Timer();
                        closeTimer.Interval = 250;
                        closeTimer.Tick += (cs, ce) => {
                            closeTimer.Stop();
                            closeTimer.Dispose();
                            if (!isWindowPinned && !this.Bounds.Contains(Cursor.Position) && !IsLeftMouseButtonDown())
                            {
                                CloseFlyout();
                            }
                        };
                        closeTimer.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("StartDragItem error: " + ex.Message);
            }
            finally
            {
                CleanupTempShortcutsDelayed(tempLnkFiles);
            }
        }

        private void SaveItemToDesktop(ClipboardItem item)
        {
            try
            {
                string dt = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (item.Type == ClipboardItemType.Image && SafeFileExists(item.ImagePath))
                {
                    string target = Path.Combine(dt, SafeGetFileName(item.ImagePath));
                    File.Copy(item.ImagePath, target, true);
                    DesktopHelper.PositionFile(target, true);
                }
                else if (item.Type == ClipboardItemType.Files && item.FilePaths != null)
                {
                    foreach (var f in item.FilePaths)
                    {
                        if (SafeFileExists(f))
                        {
                            string target = Path.Combine(dt, SafeGetFileName(f));
                            File.Copy(f, target, true);
                            DesktopHelper.PositionFile(target, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("SaveItemToDesktop error: " + ex.Message);
            }
        }

        private void ShowItemInExplorer(ClipboardItem item)
        {
            try
            {
                string path = null;
                if (item.Type == ClipboardItemType.Image) path = item.ImagePath;
                else if (item.Type == ClipboardItemType.Files && item.FilePaths != null && item.FilePaths.Count > 0) path = item.FilePaths[0];

                if (!string.IsNullOrEmpty(path) && (SafeFileExists(path) || SafeDirectoryExists(path)))
                {
                    Process.Start("explorer.exe", "/select,\"" + path + "\"");
                }
            }
            catch {}
        }

        private static string SafeGetFileName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            try { return Path.GetFileName(path); }
            catch
            {
                try
                {
                    int idx = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
                    return idx >= 0 && idx < path.Length - 1 ? path.Substring(idx + 1) : path;
                }
                catch { return path; }
            }
        }

        private static readonly Dictionary<string, bool> safeDirCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> safeFileCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static readonly object pathCacheLock = new object();

        private static bool SafeDirectoryExists(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path.IndexOf('\n') >= 0 || path.IndexOf('\r') >= 0) return false;
            try
            {
                if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return false;
            }
            catch { return false; }

            // Сетевые UNC-пути не должны блокировать UI сетевыми таймаутами
            if (path.StartsWith(@"\\") || path.StartsWith("//"))
            {
                if (path.EndsWith("\\") || path.EndsWith("/")) return true;
                string ext = SafeGetExtension(path);
                return string.IsNullOrEmpty(ext);
            }

            lock (pathCacheLock)
            {
                bool cached;
                if (safeDirCache.TryGetValue(path, out cached)) return cached;
            }

            bool exists = false;
            try { exists = Directory.Exists(path); } catch { exists = false; }

            lock (pathCacheLock)
            {
                if (safeDirCache.Count > 500) safeDirCache.Clear();
                safeDirCache[path] = exists;
            }
            return exists;
        }

        private static bool SafeFileExists(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path.IndexOf('\n') >= 0 || path.IndexOf('\r') >= 0) return false;
            try
            {
                if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return false;
            }
            catch { return false; }

            // Сетевые UNC-пути не должны блокировать UI сетевыми таймаутами
            if (path.StartsWith(@"\\") || path.StartsWith("//"))
            {
                string ext = SafeGetExtension(path);
                return !string.IsNullOrEmpty(ext);
            }

            lock (pathCacheLock)
            {
                bool cached;
                if (safeFileCache.TryGetValue(path, out cached)) return cached;
            }

            bool exists = false;
            try { exists = File.Exists(path); } catch { exists = false; }

            lock (pathCacheLock)
            {
                if (safeFileCache.Count > 500) safeFileCache.Clear();
                safeFileCache[path] = exists;
            }
            return exists;
        }

        private static string SafeGetExtension(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            try { return Path.GetExtension(path); }
            catch
            {
                try
                {
                    int idx = path.LastIndexOf('.');
                    return idx >= 0 && idx < path.Length - 1 ? path.Substring(idx) : "";
                }
                catch { return ""; }
            }
        }

        private string FormatDate(DateTime dt)
        {
            DateTime now = DateTime.Now;
            if (dt.Date == now.Date) return dt.ToString("HH:mm:ss");
            if (dt.Date == now.Date.AddDays(-1)) return "Вчера " + dt.ToString("HH:mm");
            return dt.ToString("dd.MM.yyyy HH:mm");
        }

        public static void ShowFlyout(Point cursor, IntPtr prevFg)
        {
            Logger.Log(string.Format("ShowFlyout entered: cursor=({0},{1}), prevFg={2}", cursor.X, cursor.Y, prevFg));
            try
            {
                lock (instanceLock)
                {
                    if (currentInstance != null && !currentInstance.IsDisposed)
                    {
                        bool wasVisible = currentInstance.Visible;
                        currentInstance.CloseFlyout();
                        if (wasVisible)
                        {
                            Logger.Log("ShowFlyout: toggle closed existing flyout");
                            return;
                        }
                    }

                    Screen scr = Screen.FromPoint(cursor);

                    // Если предыдущее окно - это панель задач (клик по трею), находим настоящее пользовательское окно
                    if (prevFg == IntPtr.Zero || Program.IsTaskbarOrTrayWindow(prevFg))
                    {
                        IntPtr realUserWindow = Program.FindLastActiveUserWindow(scr);
                        if (realUserWindow != IntPtr.Zero)
                        {
                            prevFg = realUserWindow;
                        }
                    }

                    currentInstance = new ClipboardFlyoutForm(prevFg);
                    currentInstance.SwitchMainTab(0);
                    currentInstance.RefreshItems();

                    // Позиционируем окно рядом с курсором мыши
                    int x = cursor.X + 14;
                    int y = cursor.Y - 36;

                    if (x + currentInstance.Width > scr.WorkingArea.Right - 8)
                    {
                        x = cursor.X - currentInstance.Width - 14;
                    }
                    if (x < scr.WorkingArea.Left + 8)
                    {
                        x = scr.WorkingArea.Left + 8;
                    }

                    if (y + currentInstance.Height > scr.WorkingArea.Bottom - 8)
                    {
                        y = scr.WorkingArea.Bottom - currentInstance.Height - 8;
                    }
                    if (y < scr.WorkingArea.Top + 8)
                    {
                        y = scr.WorkingArea.Top + 8;
                    }

                    bool isFromBottom = (y > scr.Bounds.Top + scr.Bounds.Height / 2);
                    currentInstance.UpdateSubBarLayout();
                    currentInstance.AnimateIn(new Point(x, y), isFromBottom);

                    Logger.Log(string.Format("Flyout shown near cursor at ({0},{1}) on {2} with prevFg={3}", x, y, scr.DeviceName, prevFg));
                }
            }
            catch (Exception ex)
            {
                Logger.Log("ShowFlyout EXCEPTION: " + ex.ToString());
            }
        }

        public static void ShowDockFlyout(Screen scr, string position, IntPtr prevFg, bool initialSuperHubTab = true, Rectangle markerBounds = default(Rectangle))
        {
            Logger.Log(string.Format("ShowDockFlyout entered: position={0}, prevFg={1}, superHub={2}", position, prevFg, initialSuperHubTab));
            try
            {
                lock (instanceLock)
                {
                    if (scr == null) scr = Screen.PrimaryScreen;

                    // Проверяем, существует ли переданный экран в текущей конфигурации системы
                    Screen actualScr = scr;
                    bool screenFound = false;
                    foreach (var s in Screen.AllScreens)
                    {
                        if (string.Equals(s.DeviceName, actualScr.DeviceName, StringComparison.OrdinalIgnoreCase))
                        {
                            actualScr = s;
                            screenFound = true;
                            break;
                        }
                    }
                    if (!screenFound)
                    {
                        actualScr = Screen.FromPoint(Cursor.Position) ?? Screen.PrimaryScreen;
                    }
                    scr = actualScr;

                    if (currentInstance != null && !currentInstance.IsDisposed && currentInstance.Visible)
                    {
                        currentInstance.dockMarkerBounds = markerBounds;
                        int targetTab = initialSuperHubTab ? 1 : 0;
                        if (currentInstance.currentMainTab != targetTab)
                        {
                            currentInstance.SwitchMainTab(targetTab);
                            currentInstance.UpdateSubBarLayout();
                        }
                        currentInstance.BringToFront();
                        SetForegroundWindow(currentInstance.Handle);
                        currentInstance.Activate();
                        return;
                    }

                    if (prevFg == IntPtr.Zero || Program.IsTaskbarOrTrayWindow(prevFg))
                    {
                        IntPtr realUserWindow = Program.FindLastActiveUserWindow(scr);
                        if (realUserWindow != IntPtr.Zero) prevFg = realUserWindow;
                    }

                    currentInstance = new ClipboardFlyoutForm(prevFg, true, position, scr);
                    currentInstance.dockMarkerBounds = markerBounds;

                    Rectangle work = scr.WorkingArea;

                    if (currentInstance.IsHorizontalDockMode)
                    {
                        currentInstance.MinimumSize = new Size(480, 160);
                        int initW = Math.Max(480, Config.SuperHubWidthHorizontal);
                        int initH = Math.Max(160, Config.SuperHubHeightHorizontal);
                        currentInstance.Size = new Size(initW, initH);
                    }
                    else
                    {
                        currentInstance.MinimumSize = new Size(310, 420);
                        int initW = Math.Max(310, Config.ClipboardFlyoutWidth);
                        int initH = Math.Max(420, Config.ClipboardFlyoutHeight);
                        currentInstance.Size = new Size(initW, initH);
                    }

                    currentInstance.SwitchMainTab(initialSuperHubTab ? 1 : 0);
                    currentInstance.UpdateSubBarLayout();

                    int w = currentInstance.Width;
                    int h = currentInstance.Height;

                    // Защита от превышения размеров экрана при масштабировании или маленьком дисплее
                    if (w > work.Width - 16) w = Math.Max(280, work.Width - 16);
                    if (h > work.Height - 16) h = Math.Max(currentInstance.IsHorizontalDockMode ? 160 : 320, work.Height - 16);
                    currentInstance.Size = new Size(w, h);

                    int markerCenterY;
                    if (markerBounds != Rectangle.Empty && markerBounds.Height > 0 &&
                        markerBounds.Top >= work.Top - 100 && markerBounds.Bottom <= work.Bottom + 100)
                    {
                        markerCenterY = markerBounds.Top + markerBounds.Height / 2;
                    }
                    else
                    {
                        if (position.EndsWith("Top", StringComparison.OrdinalIgnoreCase)) markerCenterY = work.Top + 64;
                        else if (position.EndsWith("Bottom", StringComparison.OrdinalIgnoreCase)) markerCenterY = work.Bottom - 64;
                        else markerCenterY = work.Top + work.Height / 2;
                    }
                    currentInstance.anchorMarkerCenterY = markerCenterY;

                    int targetX;
                    int targetY;

                    if (currentInstance.IsHorizontalDockMode)
                    {
                        int markerCenterX = (markerBounds != Rectangle.Empty && markerBounds.Width > 0)
                            ? (markerBounds.Left + markerBounds.Width / 2)
                            : (work.Left + work.Width / 2);

                        if (string.Equals(position, "TopCenter", StringComparison.OrdinalIgnoreCase))
                        {
                            targetX = markerCenterX - w / 2;
                            targetY = work.Top + 4;
                        }
                        else
                        {
                            targetX = markerCenterX - w / 2;
                            targetY = work.Bottom - h - 4;
                        }
                    }
                    else if (position.StartsWith("Corner", StringComparison.OrdinalIgnoreCase))
                    {
                        // Угловые позиции: раскрываются как обычное вертикальное меню у соответствующего угла экрана
                        if (position.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            targetX = work.Right - w - 4;
                        }
                        else // Left
                        {
                            targetX = work.Left + 4;
                        }

                        if (position.IndexOf("Bottom", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            targetY = work.Bottom - h - 4;
                        }
                        else // Top
                        {
                            targetY = work.Top + 4;
                        }
                    }
                    else
                    {
                        targetX = work.Right - w - 4;
                        targetY = markerCenterY - h / 2;

                        if (string.Equals(position, "TopCenter", StringComparison.OrdinalIgnoreCase))
                        {
                            targetX = work.Left + (work.Width - w) / 2;
                            targetY = work.Top + 4;
                        }
                        else if (position.StartsWith("Left", StringComparison.OrdinalIgnoreCase))
                        {
                            targetX = work.Left + 4;
                        }
                        else // Right (RightCenter, RightTop, RightBottom)
                        {
                            targetX = work.Right - w - 4;
                        }
                    }

                    // Строгий клампинг координат внутри рабочей области активного экрана
                    if (targetX + w > work.Right - 4) targetX = work.Right - w - 4;
                    if (targetX < work.Left + 4) targetX = work.Left + 4;
                    if (targetY + h > work.Bottom - 4) targetY = work.Bottom - h - 4;
                    if (targetY < work.Top + 4) targetY = work.Top + 4;

                    currentInstance.UpdateSubBarLayout();
                    currentInstance.LayoutControls();
                    currentInstance.AnimateIn(new Point(targetX, targetY), new Point(targetX, targetY));
                    Logger.Log(string.Format("Dock Flyout shown at ({0},{1}) on {2}", targetX, targetY, scr.DeviceName));
                }
            }
            catch (Exception ex)
            {
                Logger.Log("ShowDockFlyout EXCEPTION: " + ex.ToString());
            }
        }

        private void DragAllFilesOut()
        {
            List<string> tempLnkFiles = null;
            try
            {
                var list = ClipboardHistoryManager.Instance.GetItems(null, true);
                var sourcePaths = new List<string>();
                foreach (var it in list)
                {
                    if (it.Type == ClipboardItemType.Files && it.FilePaths != null)
                    {
                        foreach (var f in it.FilePaths) if (SafeFileExists(f) || SafeDirectoryExists(f)) sourcePaths.Add(f);
                    }
                    else if (it.Type == ClipboardItemType.Image && SafeFileExists(it.ImagePath))
                    {
                        sourcePaths.Add(it.ImagePath);
                    }
                    else if (it.Type == ClipboardItemType.Text && !string.IsNullOrEmpty(it.TextContent))
                    {
                        string tempTxt = CreateTempTextFile(it.TextContent);
                        if (!string.IsNullOrEmpty(tempTxt))
                        {
                            sourcePaths.Add(tempTxt);
                            if (tempLnkFiles == null) tempLnkFiles = new List<string>();
                            tempLnkFiles.Add(tempTxt);
                        }
                    }
                }

                if (sourcePaths.Count > 0)
                {
                    DataObject data = new DataObject();
                    data.SetData("PasteImageAsFile_InternalDrag", "true");
                    data.SetData("PasteImageAsFile_SourceItemId", "ALL");
                    bool isMove = string.Equals(Config.SuperHubDragMode, "Move", StringComparison.OrdinalIgnoreCase);
                    bool isLink = string.Equals(Config.SuperHubDragMode, "Link", StringComparison.OrdinalIgnoreCase);

                    StringCollection sc = new StringCollection();
                    if (isLink)
                    {
                        tempLnkFiles = CreateTempShortcuts(sourcePaths);
                        foreach (var lnk in tempLnkFiles) sc.Add(lnk);
                        data.SetFileDropList(sc);
                    }
                    else
                    {
                        foreach (var f in sourcePaths) sc.Add(f);
                        data.SetFileDropList(sc);
                    }

                    DragDropEffects allowedEffects = DragDropEffects.Copy;
                    if (isMove) allowedEffects = DragDropEffects.Move | DragDropEffects.Copy;
                    else if (isLink) allowedEffects = DragDropEffects.Link | DragDropEffects.Copy;

                    IsGlobalDraggingActive = true;
                    isDraggingItemOut = true;
                    GlobalDraggingItemId = "ALL";
                    GlobalDraggingPaths.Clear();
                    foreach (var f in sourcePaths) GlobalDraggingPaths.Add(f);

                    try
                    {
                        this.DoDragDrop(data, allowedEffects);
                    }
                    finally
                    {
                        isDraggingItemOut = false;
                        IsGlobalDraggingActive = false;
                        GlobalDragEndTimeTicks = DateTime.UtcNow.Ticks;

                        Rectangle b = this.Bounds;
                        b.Inflate(36, 36);
                        Point cur = Cursor.Position;
                        if (b.Contains(cur))
                        {
                            this.Activate();
                        }
                        else if (!isWindowPinned)
                        {
                            var closeTimer = new System.Windows.Forms.Timer();
                            closeTimer.Interval = 250;
                            closeTimer.Tick += (cs, ce) => {
                                closeTimer.Stop();
                                closeTimer.Dispose();
                                if (!isWindowPinned && !this.Bounds.Contains(Cursor.Position) && !IsLeftMouseButtonDown())
                                {
                                    CloseFlyout();
                                }
                            };
                            closeTimer.Start();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("ClipboardFlyoutForm DragAllFilesOut error: " + ex.Message);
            }
            finally
            {
                CleanupTempShortcutsDelayed(tempLnkFiles);
            }
        }

        /// <summary>
        /// Создаёт временный .txt файл для текстового элемента, чтобы его можно было
        /// перетащить на Рабочий стол или в любую папку Проводника как текстовый файл.
        /// </summary>
        private static string CreateTempTextFile(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "PasteImageAsFile_txt");
                if (!Directory.Exists(tempDir))
                {
                    Directory.CreateDirectory(tempDir);
                }
                else
                {
                    DateTime now = DateTime.UtcNow;
                    foreach (var old in Directory.GetFiles(tempDir, "*.txt"))
                    {
                        try
                        {
                            if ((now - File.GetCreationTimeUtc(old)).TotalMinutes > 5)
                            {
                                File.Delete(old);
                            }
                        }
                        catch {}
                    }
                }

                string clean = "";
                string trimmed = text.Trim();
                char[] invalid = Path.GetInvalidFileNameChars();
                for (int i = 0; i < trimmed.Length && clean.Length < 30; i++)
                {
                    char ch = trimmed[i];
                    if (ch == '\r' || ch == '\n' || ch == '\t')
                    {
                        if (clean.Length > 0 && clean[clean.Length - 1] != ' ') clean += ' ';
                        continue;
                    }
                    bool isInv = false;
                    for (int j = 0; j < invalid.Length; j++)
                    {
                        if (ch == invalid[j]) { isInv = true; break; }
                    }
                    if (!isInv) clean += ch;
                }
                clean = clean.Trim();
                if (string.IsNullOrEmpty(clean)) clean = "Заметка";

                string fileName = clean + " (" + DateTime.Now.ToString("HH-mm-ss") + ").txt";
                string fullPath = Path.Combine(tempDir, fileName);
                File.WriteAllText(fullPath, text, System.Text.Encoding.UTF8);
                Logger.Log("Created temp text file for DnD: " + fullPath);
                return fullPath;
            }
            catch (Exception ex)
            {
                Logger.Log("CreateTempTextFile error: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Создаёт .lnk ярлыки во временной папке для каждого исходного пути.
        /// Возвращает список путей к созданным .lnk файлам.
        /// </summary>
        private static List<string> CreateTempShortcuts(List<string> sourcePaths)
        {
            var result = new List<string>();
            string tempDir = Path.Combine(Path.GetTempPath(), "PasteImageAsFile_lnk");
            try
            {
                if (Directory.Exists(tempDir))
                {
                    DateTime now = DateTime.UtcNow;
                    foreach (var old in Directory.GetFiles(tempDir, "*.lnk"))
                    {
                        try
                        {
                            if ((now - File.GetCreationTimeUtc(old)).TotalMinutes > 2)
                            {
                                File.Delete(old);
                            }
                        }
                        catch {}
                    }
                }
                else
                {
                    Directory.CreateDirectory(tempDir);
                }
            }
            catch {}

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                Logger.Log("CreateTempShortcuts: WScript.Shell not available");
                return result;
            }

            object shell = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                foreach (var srcPath in sourcePaths)
                {
                    try
                    {
                        string cleanSrc = (srcPath ?? "").TrimEnd('\\', '/');
                        string baseName = SafeGetFileName(cleanSrc);
                        if (string.IsNullOrEmpty(baseName)) baseName = "shortcut";
                        string lnkName = baseName + ".lnk";
                        string lnkPath = Path.Combine(tempDir, lnkName);

                        // Уникализация имени если уже существует
                        int counter = 1;
                        while (File.Exists(lnkPath))
                        {
                            lnkName = baseName + " (" + counter + ").lnk";
                            lnkPath = Path.Combine(tempDir, lnkName);
                            counter++;
                        }

                        object shortcut = shellType.InvokeMember("CreateShortcut",
                            System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                        Type scType = shortcut.GetType();
                        scType.InvokeMember("TargetPath",
                            System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { cleanSrc });

                        // Устанавливаем рабочую папку в директорию цели
                        try
                        {
                            string workDir = Directory.Exists(cleanSrc) ? cleanSrc : Path.GetDirectoryName(cleanSrc);
                            if (!string.IsNullOrEmpty(workDir))
                            {
                                scType.InvokeMember("WorkingDirectory",
                                    System.Reflection.BindingFlags.SetProperty, null, shortcut, new object[] { workDir });
                            }
                        }
                        catch {}

                        scType.InvokeMember("Save",
                            System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
                        Marshal.ReleaseComObject(shortcut);

                        if (File.Exists(lnkPath))
                        {
                            result.Add(lnkPath);
                            Logger.Log("Created shortcut for DnD: " + lnkPath + " -> " + cleanSrc);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("CreateTempShortcuts item error: " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("CreateTempShortcuts error: " + ex.Message);
            }
            finally
            {
                if (shell != null)
                {
                    try { Marshal.ReleaseComObject(shell); } catch {}
                }
            }
            return result;
        }

        /// <summary>Удаляет временные .lnk файлы с задержкой 45 секунд, чтобы Проводник успел завершить копирование.</summary>
        private static void CleanupTempShortcutsDelayed(List<string> lnkFiles)
        {
            if (lnkFiles == null || lnkFiles.Count == 0) return;
            ThreadPool.QueueUserWorkItem(_ => {
                try
                {
                    Thread.Sleep(45000);
                    foreach (var f in lnkFiles)
                    {
                        try { if (File.Exists(f)) File.Delete(f); } catch {}
                    }
                }
                catch {}
            });
        }

        private static void DrawAppHeaderIcon(Graphics g, Rectangle rect)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = 18;
            int h = 18;
            int x = rect.X + (rect.Width - w) / 2;
            int y = rect.Y + (rect.Height - h) / 2;

            // Задний лист (темно-синий с голубой рамкой)
            Rectangle r1 = new Rectangle(x, y, 12, 13);
            using (var path1 = CreateRoundedPath(r1, 3))
            using (var brush1 = new SolidBrush(Color.FromArgb(14, 60, 95)))
            using (var pen1 = new Pen(Color.FromArgb(0, 162, 237), 1.5f))
            {
                g.FillPath(brush1, path1);
                g.DrawPath(pen1, path1);
            }

            // Передний лист (ярко-голубой)
            Rectangle r2 = new Rectangle(x + 5, y + 4, 12, 13);
            using (var path2 = CreateRoundedPath(r2, 3))
            using (var brush2 = new SolidBrush(Color.FromArgb(0, 162, 237)))
            using (var pen2 = new Pen(Color.FromArgb(170, 230, 255), 1.2f))
            {
                g.FillPath(brush2, path2);
                g.DrawPath(pen2, path2);
            }
        }

        private static void DrawGearIcon(Graphics g, Rectangle r, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            float outerR = 6.8f;
            float bodyR = 4.7f;
            float holeR = 1.9f;

            using (var toothPen = new Pen(color, 2.0f))
            using (var bodyPen = new Pen(color, 1.5f))
            {
                toothPen.StartCap = LineCap.Round;
                toothPen.EndCap = LineCap.Round;
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4.0;
                    float x1 = (float)(cx + Math.Cos(angle) * (bodyR - 0.2f));
                    float y1 = (float)(cy + Math.Sin(angle) * (bodyR - 0.2f));
                    float x2 = (float)(cx + Math.Cos(angle) * outerR);
                    float y2 = (float)(cy + Math.Sin(angle) * outerR);
                    g.DrawLine(toothPen, x1, y1, x2, y2);
                }
                g.DrawEllipse(bodyPen, cx - bodyR, cy - bodyR, bodyR * 2f, bodyR * 2f);
                g.DrawEllipse(bodyPen, cx - holeR, cy - holeR, holeR * 2f, holeR * 2f);
            }
        }

        private static void DrawPinIcon(Graphics g, Rectangle r, Color color, bool isPinned)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;

            using (var pen = new Pen(color, 1.3f))
            using (var brush = new SolidBrush(color))
            {
                // 1. Верхняя шляпка-диск
                float topW = 9.0f;
                float topH = 2.4f;
                RectangleF topRect = new RectangleF(cx - topW / 2f, cy - 6.5f, topW, topH);
                using (var topPath = CreateRoundedPath(Rectangle.Round(topRect), 1))
                {
                    if (isPinned) g.FillPath(brush, topPath);
                    else g.DrawPath(pen, topPath);
                }

                // 2. Тело булавки (коническая форма)
                PointF[] body = new PointF[] {
                    new PointF(cx - 3.2f, cy - 4.1f),
                    new PointF(cx + 3.2f, cy - 4.1f),
                    new PointF(cx + 1.8f, cy - 0.2f),
                    new PointF(cx - 1.8f, cy - 0.2f)
                };
                if (isPinned) g.FillPolygon(brush, body);
                else g.DrawPolygon(pen, body);

                // 3. Опорный упор кнопки (фланец)
                float rimW = 8.0f;
                float rimH = 2.0f;
                RectangleF rimRect = new RectangleF(cx - rimW / 2f, cy - 0.2f, rimW, rimH);
                using (var rimPath = CreateRoundedPath(Rectangle.Round(rimRect), 1))
                {
                    if (isPinned) g.FillPath(brush, rimPath);
                    else g.DrawPath(pen, rimPath);
                }

                // 4. Острая игла
                using (var needlePen = new Pen(color, 1.5f))
                {
                    needlePen.EndCap = LineCap.Round;
                    g.DrawLine(needlePen, cx, cy + 1.8f, cx, cy + 6.8f);
                }
            }
        }

        private static void DrawWin11PinIcon(Graphics g, Rectangle r, Color color, bool isPinned)
        {
            GraphicsState state = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                g.TranslateTransform(cx, cy);
                g.RotateTransform(45f);

                using (var pen = new Pen(color, 1.35f))
                using (var brush = new SolidBrush(color))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;

                    // Верхняя шляпка булавки
                    g.DrawLine(pen, -3.5f, -5.8f, 3.5f, -5.8f);

                    // Трапециевидное тело булавки
                    PointF[] body = new PointF[] {
                        new PointF(-2.5f, -5.8f),
                        new PointF(2.5f, -5.8f),
                        new PointF(3.6f, 0.2f),
                        new PointF(-3.6f, 0.2f)
                    };
                    if (isPinned)
                    {
                        g.FillPolygon(brush, body);
                    }
                    else
                    {
                        g.DrawPolygon(pen, body);
                    }

                    // Нижний фланец
                    g.DrawLine(pen, -4.4f, 0.2f, 4.4f, 0.2f);

                    // Игла булавки
                    g.DrawLine(pen, 0f, 0.4f, 0f, 6.2f);
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        private static void DrawMoreDotsIcon(Graphics g, Rectangle r, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            float dotR = 1.35f;
            using (var brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, cx - 5.5f - dotR, cy - dotR, dotR * 2f, dotR * 2f);
                g.FillEllipse(brush, cx - dotR, cy - dotR, dotR * 2f, dotR * 2f);
                g.FillEllipse(brush, cx + 5.5f - dotR, cy - dotR, dotR * 2f, dotR * 2f);
            }
        }

        private static void DrawMinimizeIcon(Graphics g, Rectangle r, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cy = r.Y + r.Height / 2;
            using (var pen = new Pen(color, 1.3f))
            {
                g.DrawLine(pen, r.X + 8, cy, r.Right - 8, cy);
            }
        }

        private static void DrawCloseIcon(Graphics g, Rectangle r, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, 1.4f))
            {
                g.DrawLine(pen, r.X + 8, r.Y + 8, r.Right - 8, r.Bottom - 8);
                g.DrawLine(pen, r.Right - 8, r.Y + 8, r.X + 8, r.Bottom - 8);
            }
        }

        private static void DrawCopyIcon(Graphics g, Rectangle r, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, 1.3f))
            {
                int w = 9;
                int h = 10;
                int x = r.X + (r.Width - w - 3) / 2;
                int y = r.Y + (r.Height - h - 3) / 2;

                g.DrawLine(pen, x + 3, y, x + w + 3, y);
                g.DrawLine(pen, x + w + 3, y, x + w + 3, y + h);
                g.DrawLine(pen, x + w, y + h + 3, x + w + 3, y + h + 3);

                using (var path = CreateRoundedPath(new Rectangle(x, y + 3, w, h), 2))
                {
                    g.DrawPath(pen, path);
                }
            }
        }

        private static void DrawLinkIcon(Graphics g, Rectangle r, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            using (var pen = new Pen(color, 1.4f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                // Диагональная стрелка ↗
                g.DrawLine(pen, cx - 4f, cy + 4f, cx + 4.5f, cy - 4.5f);
                // Наконечник стрелки
                g.DrawLine(pen, cx + 1f, cy - 4.5f, cx + 4.5f, cy - 4.5f);
                g.DrawLine(pen, cx + 4.5f, cy - 4.5f, cx + 4.5f, cy - 1f);

                // Нижний уголок рамки (символизирует исходный объект/ярлык)
                g.DrawLine(pen, cx - 4.5f, cy - 1f, cx - 4.5f, cy + 4.5f);
                g.DrawLine(pen, cx - 4.5f, cy + 4.5f, cx + 1f, cy + 4.5f);
            }
        }

        private static void DrawEyeIcon(Graphics g, Rectangle r, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            using (var pen = new Pen(color, 1.3f))
            using (var brush = new SolidBrush(color))
            {
                RectangleF eyeRect = new RectangleF(cx - 6.5f, cy - 4.5f, 13f, 9f);
                g.DrawArc(pen, eyeRect, 0, 180);
                g.DrawArc(pen, eyeRect, 180, 180);
                g.FillEllipse(brush, cx - 2f, cy - 2f, 4f, 4f);
            }
        }

        private static void DrawTrashIcon(Graphics g, Rectangle r, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            using (var pen = new Pen(color, 1.3f))
            {
                g.DrawLine(pen, cx - 6f, cy - 5f, cx + 6f, cy - 5f);
                g.DrawLine(pen, cx - 2.5f, cy - 7f, cx + 2.5f, cy - 7f);
                PointF[] body = new PointF[] {
                    new PointF(cx - 4.5f, cy - 4f),
                    new PointF(cx - 3.8f, cy + 6f),
                    new PointF(cx + 3.8f, cy + 6f),
                    new PointF(cx + 4.5f, cy - 4f)
                };
                g.DrawPolygon(pen, body);
                g.DrawLine(pen, cx - 1.5f, cy - 2f, cx - 1.5f, cy + 4f);
                g.DrawLine(pen, cx + 1.5f, cy - 2f, cx + 1.5f, cy + 4f);
            }
        }

        private static void DrawSearchIcon(Graphics g, Rectangle r, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, 1.4f))
            {
                float cx = r.X + r.Width / 2f - 1.5f;
                float cy = r.Y + r.Height / 2f - 1.5f;
                float radius = 4.5f;
                g.DrawEllipse(pen, cx - radius, cy - radius, radius * 2, radius * 2);
                g.DrawLine(pen, cx + 3.2f, cy + 3.2f, cx + 7f, cy + 7f);
            }
        }

        private static void DrawInfoIcon(Graphics g, Rectangle r, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            using (var pen = new Pen(color, 1.3f))
            using (var brush = new SolidBrush(color))
            {
                g.DrawEllipse(pen, cx - 6.5f, cy - 6.5f, 13f, 13f);
                g.FillEllipse(brush, cx - 1f, cy - 4.2f, 2f, 2f);
                g.DrawLine(pen, cx, cy - 1f, cx, cy + 3.8f);
            }
        }

        private static void DrawTextDocThumbnail(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Подложка 54x54
            using (var bgPath = CreateRoundedPath(r, 6))
            using (var bgBrush = new SolidBrush(Color.FromArgb(17, 34, 56)))
            {
                g.FillPath(bgBrush, bgPath);
            }

            int docW = 28;
            int docH = 34;
            int docX = r.X + (r.Width - docW) / 2;
            int docY = r.Y + (r.Height - docH) / 2;

            Color iconBlue = Color.FromArgb(0, 162, 237);
            using (var pen = new Pen(iconBlue, 1.8f))
            using (var docPath = CreateRoundedPath(new Rectangle(docX, docY, docW, docH), 4))
            {
                g.DrawPath(pen, docPath);

                // Три горизонтальные полоски текста
                using (var linePen = new Pen(iconBlue, 2f))
                {
                    linePen.StartCap = LineCap.Round;
                    linePen.EndCap = LineCap.Round;
                    g.DrawLine(linePen, docX + 6, docY + 10, docX + docW - 6, docY + 10);
                    g.DrawLine(linePen, docX + 6, docY + 17, docX + docW - 6, docY + 17);
                    g.DrawLine(linePen, docX + 6, docY + 24, docX + docW - 10, docY + 24);
                }
            }
        }

        private static void DrawPdfDocThumbnail(Graphics g, Rectangle r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Подложка со скруглением
            using (var bgPath = CreateRoundedPath(r, 6))
            using (var bgBrush = new SolidBrush(Color.FromArgb(24, 36, 50)))
            {
                g.FillPath(bgBrush, bgPath);
            }

            // Белый лист со срезанным уголком
            int docW = 32;
            int docH = 38;
            int docX = r.X + (r.Width - docW) / 2;
            int docY = r.Y + (r.Height - docH) / 2;

            using (var whiteBrush = new SolidBrush(Color.FromArgb(245, 245, 245)))
            using (var sheetPath = CreateRoundedPath(new Rectangle(docX, docY, docW, docH), 3))
            {
                g.FillPath(whiteBrush, sheetPath);
            }

            // Красный шильдик PDF
            int badgeW = 24;
            int badgeH = 14;
            int badgeX = r.X + (r.Width - badgeW) / 2;
            int badgeY = docY + 15;

            using (var redPath = CreateRoundedPath(new Rectangle(badgeX, badgeY, badgeW, badgeH), 2))
            using (var redBrush = new SolidBrush(Color.FromArgb(220, 38, 38)))
            {
                g.FillPath(redBrush, redPath);
            }

            using (var textBrush = new SolidBrush(Color.White))
            using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString("PDF", font, textBrush, new RectangleF(badgeX, badgeY, badgeW, badgeH), sf);
            }
        }

        public static void GenerateScreenshots(string outDir)
        {
            try
            {
                isGeneratingScreenshots = true;
                if (string.IsNullOrEmpty(outDir))
                {
                    outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
                }
                else if (!Path.IsPathRooted(outDir))
                {
                    outDir = Path.GetFullPath(outDir);
                }
                if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
                Logger.Log("GenerateScreenshots target dir: " + outDir);

                ClipboardFlyoutForm form = new ClipboardFlyoutForm(IntPtr.Zero);
                form.isWindowPinned = true;
                if (form.mouseLeaveTimer != null) form.mouseLeaveTimer.Stop();
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(120, 120);
                form.SwitchMainTab(0);
                form.UpdateAcrylicBackdrop(form.Location);

                int capturePhase = 0;
                System.Windows.Forms.Timer captureTimer = new System.Windows.Forms.Timer();
                captureTimer.Interval = 300;
                captureTimer.Tick += (s, e) => {
                    try
                    {
                        if (capturePhase == 0)
                        {
                            capturePhase = 1;
                            form.UpdateAcrylicBackdrop(form.Location);
                            form.Refresh();
                            string clipPath = Path.Combine(outDir, "flyout_clipboard.png");
                            CaptureFormBitmap(form, clipPath);
                            Logger.Log("Saved flyout_clipboard.png to " + clipPath);

                            // Сохраняем также наглядный превью-снимок единого двухцветного маркера у края экрана (вертикальный и горизонтальный)
                            try
                            {
                                using (Bitmap markerPreview = new Bitmap(80, 180))
                                using (Graphics mg = Graphics.FromImage(markerPreview))
                                {
                                    mg.SmoothingMode = SmoothingMode.AntiAlias;
                                    mg.Clear(Color.FromArgb(24, 24, 24));
                                    int mx = 68;
                                    int my = 21;
                                    int mw = 5;
                                    int mh = 138;
                                    int half = mh / 2;
                                    using (var path = CreateRoundedPath(new Rectangle(mx, my, mw, mh), 2))
                                    {
                                        mg.SetClip(path);
                                        using (var b1 = new SolidBrush(ThemeHelper.Accent))
                                        {
                                            mg.FillRectangle(b1, mx, my, mw, half);
                                        }
                                        using (var b2 = new SolidBrush(ThemeHelper.SuperHubSecondaryAccent))
                                        {
                                            mg.FillRectangle(b2, mx, my + half, mw, mh - half);
                                        }
                                        mg.ResetClip();
                                    }
                                    markerPreview.Save(Path.Combine(outDir, "superhub_marker.png"), System.Drawing.Imaging.ImageFormat.Png);
                                }

                                using (Bitmap markerHoriz = new Bitmap(180, 80))
                                using (Graphics hg = Graphics.FromImage(markerHoriz))
                                {
                                    hg.SmoothingMode = SmoothingMode.AntiAlias;
                                    hg.Clear(Color.FromArgb(24, 24, 24));
                                    int hx = 21;
                                    int hy = 68;
                                    int hw = 138;
                                    int hh = 5;
                                    int hhalf = hw / 2;
                                    using (var hpath = CreateRoundedPath(new Rectangle(hx, hy, hw, hh), 2))
                                    {
                                        hg.SetClip(hpath);
                                        using (var b1 = new SolidBrush(ThemeHelper.Accent))
                                        {
                                            hg.FillRectangle(b1, hx, hy, hhalf, hh);
                                        }
                                        using (var b2 = new SolidBrush(ThemeHelper.SuperHubSecondaryAccent))
                                        {
                                            hg.FillRectangle(b2, hx + hhalf, hy, hw - hhalf, hh);
                                        }
                                        hg.ResetClip();
                                    }
                                    markerHoriz.Save(Path.Combine(outDir, "superhub_marker_horizontal.png"), System.Drawing.Imaging.ImageFormat.Png);
                                }

                                using (Bitmap markerCorner = new Bitmap(80, 80))
                                using (Graphics cg = Graphics.FromImage(markerCorner))
                                {
                                    cg.SmoothingMode = SmoothingMode.AntiAlias;
                                    cg.Clear(Color.FromArgb(24, 24, 24));
                                    int cr = 46;
                                    using (var b1 = new SolidBrush(ThemeHelper.Accent))
                                    {
                                        cg.FillPie(b1, 80 - cr, 80 - cr, cr * 2, cr * 2, 180, 45);
                                    }
                                    using (var b2 = new SolidBrush(ThemeHelper.SuperHubSecondaryAccent))
                                    {
                                        cg.FillPie(b2, 80 - cr, 80 - cr, cr * 2, cr * 2, 225, 45);
                                    }
                                    markerCorner.Save(Path.Combine(outDir, "superhub_marker_corner.png"), System.Drawing.Imaging.ImageFormat.Png);
                                }
                            }
                            catch {}

                            form.SwitchMainTab(1);
                            form.LayoutControls();
                            form.UpdateAcrylicBackdrop(form.Location);
                            form.Refresh();
                        }
                        else if (capturePhase == 1)
                        {
                            capturePhase = 2;
                            string hubPath = Path.Combine(outDir, "flyout_superhub.png");
                            CaptureFormBitmap(form, hubPath);
                            Logger.Log("Saved flyout_superhub.png to " + hubPath);

                            // Проверка ресайза: растягиваем окно по ширине и высоте (как при ручном растягивании пользователем)
                            form.MinimumSize = new Size(300, 350);
                            form.Size = new Size(480, 580);
                            form.LayoutControls();
                            form.UpdateAcrylicBackdrop(form.Location);
                            form.Refresh();
                        }
                        else if (capturePhase == 2)
                        {
                            capturePhase = 3;
                            string resizedPath = Path.Combine(outDir, "flyout_resized.png");
                            CaptureFormBitmap(form, resizedPath);
                            Logger.Log("Saved flyout_resized.png to " + resizedPath);

                            // Переводим форму в горизонтальный док снизу и делаем снимок с карточками
                            form.isDockFlyout = true;
                            form.dockPositionMode = "BottomCenter";
                            form.MinimumSize = new Size(480, 160);
                            form.Size = new Size(720, 230);
                            form.Location = new Point(100, 350);
                            form.SwitchMainTab(0);
                            form.LayoutControls();
                            form.UpdateAcrylicBackdrop(form.Location);
                            form.Refresh();
                        }
                        else if (capturePhase == 3)
                        {
                            capturePhase = 4;
                            string dockPath = Path.Combine(outDir, "flyout_superhub_dock.png");
                            CaptureFormBitmap(form, dockPath);
                            Logger.Log("Saved flyout_superhub_dock.png to " + dockPath);

                            // Переводим форму в угловой режим снизу слева (стандартное меню у угла) - SuperHub
                            form.dockPositionMode = "CornerBottomLeft";
                            form.MinimumSize = new Size(310, 420);
                            form.Size = new Size(360, 520);
                            form.Location = new Point(100, 100);
                            form.SwitchMainTab(1);
                            form.LayoutControls();
                            form.UpdateAcrylicBackdrop(form.Location);
                            form.Refresh();
                        }
                        else if (capturePhase == 4)
                        {
                            capturePhase = 5;
                            string cornerPath = Path.Combine(outDir, "flyout_superhub_corner.png");
                            CaptureFormBitmap(form, cornerPath);
                            Logger.Log("Saved flyout_superhub_corner.png to " + cornerPath);

                            // В угловом режиме переключаем на Буфер обмена (с карточками)
                            form.SwitchMainTab(0);
                            form.LayoutControls();
                            form.UpdateAcrylicBackdrop(form.Location);
                            form.Refresh();
                        }
                        else if (capturePhase == 5)
                        {
                            capturePhase = 6;
                            captureTimer.Stop();
                            string cornerClipPath = Path.Combine(outDir, "flyout_clipboard_corner.png");
                            CaptureFormBitmap(form, cornerClipPath);
                            Logger.Log("Saved flyout_clipboard_corner.png to " + cornerClipPath);

                            form.Hide();

                            MainForm mForm = new MainForm();
                            mForm.StartPosition = FormStartPosition.Manual;
                            mForm.Location = new Point(140, 140);
                            mForm.Show();
                            mForm.Refresh();

                            int setPhase = 0;
                            System.Windows.Forms.Timer settingsTimer = new System.Windows.Forms.Timer();
                            settingsTimer.Interval = 250;
                            settingsTimer.Tick += (st, se) => {
                                try
                                {
                                    if (setPhase == 0)
                                    {
                                        setPhase = 1;
                                        string setPath = Path.Combine(outDir, "settings_main.png");
                                        CaptureFormBitmap(mForm, setPath);
                                        Logger.Log("Saved settings_main.png to " + setPath);

                                        mForm.SwitchTab(1);
                                        mForm.Refresh();
                                    }
                                    else if (setPhase == 1)
                                    {
                                        setPhase = 2;
                                        string clipPath = Path.Combine(outDir, "settings_clipboard.png");
                                        CaptureFormBitmap(mForm, clipPath);
                                        Logger.Log("Saved settings_clipboard.png to " + clipPath);

                                        mForm.SwitchTab(2);
                                        mForm.Refresh();
                                    }
                                    else if (setPhase == 2)
                                    {
                                        settingsTimer.Stop();
                                        string setHubPath = Path.Combine(outDir, "settings_superhub.png");
                                        CaptureFormBitmap(mForm, setHubPath);
                                        Logger.Log("Saved settings_superhub.png to " + setHubPath);

                                        mForm.Dispose();
                                        form.Close();
                                        Application.ExitThread();
                                    }
                                }
                                catch (Exception exSt)
                                {
                                    Logger.Log("SettingsTimer error: " + exSt.Message);
                                    mForm.Dispose();
                                    form.Close();
                                    Application.ExitThread();
                                }
                            };
                            settingsTimer.Start();
                        }
                    }
                    catch (Exception exTimer)
                    {
                        Logger.Log("CaptureTimer error: " + exTimer.ToString());
                        form.Close();
                        Application.ExitThread();
                    }
                };

                Logger.Log("Before form.Show");
                try
                {
                    form.Show();
                    form.BringToFront();
                    form.Update();
                    Logger.Log("After form.Show & Update");
                }
                catch (Exception exShow)
                {
                    Logger.Log("form.Show EXCEPTION: " + exShow.ToString());
                    throw;
                }
                Logger.Log("Before captureTimer.Start");
                captureTimer.Start();

                Logger.Log("Before Application.Run(form)");
                try
                {
                    Application.Run(form);
                    Logger.Log("After Application.Run(form)");
                }
                catch (Exception exRun)
                {
                    Logger.Log("Application.Run EXCEPTION: " + exRun.ToString());
                    throw;
                }
                Logger.Log("GenerateScreenshots finished successfully");
            }
            catch (Exception ex)
            {
                Logger.Log("GenerateScreenshots error: " + ex.ToString());
            }
            finally
            {
                isGeneratingScreenshots = false;
            }
        }

        private static void CaptureFormBitmap(Form f, string path)
        {
            try
            {
                ClipboardFlyoutForm cff = f as ClipboardFlyoutForm;
                bool isCorner = cff != null && cff.IsCornerDockMode;
                Logger.Log(string.Format("CaptureFormBitmap path={0}, f.Region={1}, isCorner={2}, size={3}x{4}",
                    Path.GetFileName(path),
                    f.Region != null ? "NOT_NULL" : "NULL",
                    isCorner,
                    f.Width,
                    f.Height));

                using (Bitmap bmp = new Bitmap(f.Width, f.Height))
                {
                    bool captured = false;
                    if (f.Visible && f.IsHandleCreated)
                    {
                        try
                        {
                            using (Graphics sg = Graphics.FromImage(bmp))
                            {
                                Point scrPt = f.PointToScreen(Point.Empty);
                                sg.CopyFromScreen(scrPt, Point.Empty, f.Size, CopyPixelOperation.SourceCopy);
                                captured = true;
                            }
                        }
                        catch {}
                    }
                    if (!captured)
                    {
                        f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                    }

                    if (isCorner)
                    {
                        using (Bitmap masked = new Bitmap(f.Width, f.Height))
                        using (Graphics g = Graphics.FromImage(masked))
                        {
                            g.Clear(Color.Transparent);
                            using (var pathRegion = CreateCornerPiePath(f.Width, f.Height, cff.dockPositionMode))
                            using (var rgn = new Region(pathRegion))
                            {
                                g.Clip = rgn;
                                g.DrawImage(bmp, 0, 0);
                            }
                            masked.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                            Logger.Log("CaptureFormBitmap: successfully saved masked corner PNG to " + path);
                            return;
                        }
                    }
                    else if (f.Region != null)
                    {
                        using (Bitmap masked = new Bitmap(f.Width, f.Height))
                        using (Graphics g = Graphics.FromImage(masked))
                        {
                            g.Clear(Color.Transparent);
                            g.Clip = f.Region;
                            g.DrawImage(bmp, 0, 0);
                            masked.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                            return;
                        }
                    }
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("CaptureFormBitmap DrawToBitmap error: " + ex.Message);
                try
                {
                    using (Bitmap bmp = new Bitmap(f.Width, f.Height))
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            Point scrPt = f.PointToScreen(Point.Empty);
                            g.CopyFromScreen(scrPt, Point.Empty, f.Size, CopyPixelOperation.SourceCopy);
                        }
                        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                catch (Exception ex2)
                {
                    Logger.Log("CaptureFormBitmap CopyFromScreen error: " + ex2.ToString());
                }
            }
        }
    }
}
