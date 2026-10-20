using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PasteImageAsFile
{
    /// <summary>
    /// Автономный легковесный маркер (Dock Strip / Marker) у края или угла экрана.
    /// Поддерживает работу на всех экранах (AllScreens) или выбранном экране.
    /// При наведении, клике или Drop активирует единое всплывающее окно на соответствующем экране.
    /// </summary>
    public class SuperHubDockForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out DesktopHelper.POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("shell32.dll")]
        private static extern int SHQueryUserNotificationState(out QUERY_USER_NOTIFICATION_STATE pquns);

        private enum QUERY_USER_NOTIFICATION_STATE
        {
            QUNS_NOT_PRESENT = 1,
            QUNS_BUSY = 2,
            QUNS_RUNNING_D3D_FULL_SCREEN = 3,
            QUNS_PRESENTATION_MODE = 4,
            QUNS_ACCEPTS_NOTIFICATIONS = 5,
            QUNS_QUIET_TIME = 6,
            QUNS_APP = 7
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int GWL_STYLE = -16;
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_MAXIMIZE = 0x01000000;

        private const int VK_LBUTTON = 0x01;

        // Размеры маркера у края экрана
        private const int BarThickness = 4;
        private const int BarLength = 92;
        private const int SplitBarThickness = 5;
        private const int SplitBarLength = 138;
        private const int SplitGap = 3;
        private const int CornerRadius = 16;
        private const int SplitCornerRadius = 22;

        private static readonly List<SuperHubDockForm> activeDocks = new List<SuperHubDockForm>();
        private static readonly object instanceLock = new object();
        private static bool isDisplayListenerRegistered = false;

        private System.Windows.Forms.Timer pollTimer;
        private int hoverExpandCounter = 0;
        private bool isMouseHovering = false;
        private int hoveredHalf = 0; // 0 = нет, 1 = первая часть (Буфер обмена), 2 = вторая часть (SuperHub)

        public Screen AttachedScreen { get; private set; }

        public static SuperHubDockForm Instance
        {
            get
            {
                lock (instanceLock)
                {
                    if (activeDocks.Count == 0 || activeDocks[0].IsDisposed)
                    {
                        SyncAllDocks();
                    }
                    return activeDocks.Count > 0 ? activeDocks[0] : null;
                }
            }
        }

        private static System.Windows.Forms.Timer syncDebounceTimer;
        private static readonly object syncTimerLock = new object();

        public static void ScheduleSyncAllDocks(int delayMs = 250)
        {
            Program.PostToUi(delegate {
                lock (syncTimerLock)
                {
                    if (syncDebounceTimer == null)
                    {
                        syncDebounceTimer = new System.Windows.Forms.Timer();
                        syncDebounceTimer.Tick += delegate(object s, EventArgs e) {
                            syncDebounceTimer.Stop();
                            try { SyncAllDocks(); } catch (Exception ex) { Logger.Log("SyncAllDocks error: " + ex.Message); }
                        };
                    }
                    syncDebounceTimer.Stop();
                    syncDebounceTimer.Interval = Math.Max(50, delayMs);
                    syncDebounceTimer.Start();
                }
            });
        }

        public static void SyncAllDocks()
        {
            Program.PostToUi(delegate {
                lock (instanceLock)
                {
                    if (!isDisplayListenerRegistered)
                    {
                        try
                        {
                            SystemEvents.DisplaySettingsChanged += delegate(object s, EventArgs e) {
                                try { ScheduleSyncAllDocks(300); } catch {}
                            };
                            isDisplayListenerRegistered = true;
                        }
                        catch {}
                    }

                    foreach (var dock in activeDocks)
                    {
                        try
                        {
                            dock.StopPollTimer();
                            dock.Hide();
                            dock.Dispose();
                        }
                        catch {}
                    }
                    activeDocks.Clear();

                    if (!Config.SuperHubEnabled) return;

                    Screen[] screens = Screen.AllScreens;
                    foreach (var s in screens)
                    {
                        if (Config.IsScreenSelected(s.DeviceName))
                        {
                            var d = new SuperHubDockForm(s);
                            d.Show();
                            activeDocks.Add(d);
                        }
                    }

                    // Fallback: если ни один экран не был выбран или экраны изменились
                    if (activeDocks.Count == 0 && screens.Length > 0)
                    {
                        var d = new SuperHubDockForm(Screen.PrimaryScreen);
                        d.Show();
                        activeDocks.Add(d);
                    }
                }
            });
        }

        public Screen GetRealScreen()
        {
            try
            {
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    Screen s = Screen.FromHandle(this.Handle);
                    if (s != null) return s;
                }
            }
            catch {}
            try
            {
                return Screen.FromPoint(new Point(this.Left + this.Width / 2, this.Top + this.Height / 2));
            }
            catch {}
            return Screen.PrimaryScreen;
        }

        public static void RepositionAllDocks()
        {
            lock (instanceLock)
            {
                if (activeDocks.Count == 0)
                {
                    SyncAllDocks();
                    return;
                }
                foreach (var d in activeDocks)
                {
                    if (!d.IsDisposed) d.PositionCollapsed();
                }
            }
        }

        public static void RefreshAllDocks()
        {
            lock (instanceLock)
            {
                foreach (var d in activeDocks)
                {
                    if (!d.IsDisposed) d.Invalidate();
                }
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE - не перехватывает фокус ввода
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW - не виден в Alt+Tab
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST - поверх окон
                return cp;
            }
        }

        public SuperHubDockForm(Screen screen = null)
        {
            this.AttachedScreen = screen ?? Screen.PrimaryScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(BarThickness, BarLength);
            this.TopMost = true;
            this.AllowDrop = true;
            this.DoubleBuffered = true;
            this.Icon = Program.GetAppIcon(32);
            this.MinimumSize = new Size(1, 1);
            this.BackColor = ThemeHelper.Accent;
            this.Cursor = Cursors.Hand;

            this.MouseClick += delegate(object s, MouseEventArgs e) {
                Point screenPt = this.PointToScreen(e.Location);
                bool openSh = ShouldOpenSuperHubAtScreenPoint(screenPt);
                ExpandShelf(true, openSh);
            };

            this.MouseEnter += (s, e) => {
                isMouseHovering = true;
                HandleMarkerHover(Cursor.Position);
            };
            this.MouseMove += (s, e) => {
                isMouseHovering = true;
                Point screenPt = this.PointToScreen(e.Location);
                HandleMarkerHover(screenPt);
            };
            this.MouseLeave += (s, e) => {
                isMouseHovering = false;
                hoveredHalf = 0;
                this.Invalidate();
            };

            this.DragEnter += OnDragEnter;
            this.DragOver += (s, e) => {
                if (ClipboardFlyoutForm.IsOurInternalDrag(e.Data))
                {
                    e.Effect = DragDropEffects.None;
                    return;
                }
                e.Effect = DragDropEffects.Copy;
            };
            this.DragDrop += OnDragDrop;

            ThemeHelper.ThemeChanged += () => {
                if (!this.IsDisposed && this.IsHandleCreated)
                {
                    this.BeginInvoke((Action)(() => {
                        this.BackColor = ThemeHelper.Accent;
                        this.Invalidate();
                    }));
                }
            };

            PositionCollapsed();

            // Таймер опроса курсора (детекция наведения и перетаскивания файлов к краю)
            pollTimer = new System.Windows.Forms.Timer();
            pollTimer.Interval = 75;
            pollTimer.Tick += OnPollTick;
            pollTimer.Start();
        }

        public void StopPollTimer()
        {
            if (pollTimer != null)
            {
                try { pollTimer.Stop(); } catch {}
                try { pollTimer.Dispose(); } catch {}
                pollTimer = null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopPollTimer();
            }
            base.Dispose(disposing);
        }

        private int GetHalfForScreenPoint(Point screenPt)
        {
            if (!Config.SuperHubSplitMarker) return 2;
            if (IsCornerPosition())
            {
                string pos = Config.GetScreenPosition(this.AttachedScreen != null ? this.AttachedScreen.DeviceName : null, Config.SuperHubPosition);
                int relX = screenPt.X - this.Left;
                int relY = screenPt.Y - this.Top;
                int w = this.Width;
                int h = this.Height;
                if (string.Equals(pos, "CornerBottomRight", StringComparison.OrdinalIgnoreCase))
                {
                    return (relX < relY) ? 1 : 2;
                }
                else if (string.Equals(pos, "CornerBottomLeft", StringComparison.OrdinalIgnoreCase))
                {
                    return (relX + relY < w) ? 1 : 2;
                }
                else if (string.Equals(pos, "CornerTopRight", StringComparison.OrdinalIgnoreCase))
                {
                    return (relX + relY >= w) ? 1 : 2;
                }
                else // CornerTopLeft
                {
                    return (relX > relY) ? 1 : 2;
                }
            }
            else if (IsHorizontalPosition())
            {
                int midX = this.Left + this.Width / 2;
                return (screenPt.X < midX) ? 1 : 2;
            }
            else
            {
                int midY = this.Top + this.Height / 2;
                return (screenPt.Y < midY) ? 1 : 2;
            }
        }

        private bool ShouldOpenSuperHubAtScreenPoint(Point screenPt)
        {
            if (!Config.SuperHubSplitMarker) return true;
            // Первая часть (верхняя / левая, синяя) -> Буфер обмена (false)
            // Вторая часть (нижняя / правая, изумрудная) -> SuperHub (true)
            return GetHalfForScreenPoint(screenPt) == 2;
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (ClipboardFlyoutForm.IsOurInternalDrag(e.Data))
            {
                e.Effect = DragDropEffects.None;
                return;
            }
            if (ImageDropHelper.HasDropData(e.Data))
            {
                e.Effect = DragDropEffects.Copy;
                ExpandShelf(false, true);
            }
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            if (ClipboardFlyoutForm.IsOurInternalDrag(e.Data))
            {
                e.Effect = DragDropEffects.None;
                return;
            }
            try
            {
                ClipboardHistoryManager.Instance.AddFromDataObject(e.Data, true);
                ExpandShelf(false, true);
            }
            catch (Exception ex)
            {
                Logger.Log("SuperHubDockForm OnDragDrop error: " + ex.Message);
            }
        }

        private static readonly uint CurrentPid = (uint)Process.GetCurrentProcess().Id;
        private static readonly Dictionary<string, FullscreenCacheEntry> fullScreenCache = new Dictionary<string, FullscreenCacheEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly object fsLock = new object();

        private struct FullscreenCacheEntry
        {
            public bool IsFullscreen;
            public long ExpiryTicks;
        }

        public static bool IsFullScreenGameOrAppActive(Screen screen)
        {
            string scrKey = (screen != null) ? screen.DeviceName : "primary";
            long now = DateTime.UtcNow.Ticks;
            lock (fsLock)
            {
                FullscreenCacheEntry entry;
                if (fullScreenCache.TryGetValue(scrKey, out entry))
                {
                    if (now < entry.ExpiryTicks) return entry.IsFullscreen;
                }
            }

            bool result = CheckIsFullScreen(screen);
            lock (fsLock)
            {
                FullscreenCacheEntry newEntry;
                newEntry.IsFullscreen = result;
                newEntry.ExpiryTicks = now + TimeSpan.FromMilliseconds(500).Ticks;
                fullScreenCache[scrKey] = newEntry;
            }
            return result;
        }

        private static bool CheckIsFullScreen(Screen screen)
        {
            try
            {
                // 1. Проверка через Shell API (Direct3D/DirectX/Vulkan эксклюзивные полноэкранные игры)
                QUERY_USER_NOTIFICATION_STATE state;
                if (SHQueryUserNotificationState(out state) == 0)
                {
                    if (state == QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN)
                    {
                        return true;
                    }
                }

                // 2. Проверка активного переднего окна (на случай Borderless Fullscreen)
                IntPtr fg = GetForegroundWindow();
                if (fg != IntPtr.Zero)
                {
                    // Исключаем окна собственного процесса
                    uint fgPid;
                    GetWindowThreadProcessId(fg, out fgPid);
                    if (fgPid == CurrentPid) return false;

                    // Если окно свернуто (минимизировано), оно не является активной игрой
                    if (IsIconic(fg)) return false;

                    var sb = new System.Text.StringBuilder(256);
                    GetClassName(fg, sb, sb.Capacity);
                    string cls = sb.ToString();
                    if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd" ||
                        cls == "Windows.UI.Core.CoreWindow" || cls == "DV2ControlHost" ||
                        cls == "Chrome_WidgetWin_1" || cls == "MozillaWindowClass" || cls == "ApplicationFrameWindow" ||
                        cls == "CabinetWClass" || cls == "Notepad" ||
                        cls.StartsWith("XamlExplorerHostIslandWindow", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    int style = GetWindowLong(fg, GWL_STYLE);

                    // Обычные развернутые окна приложений имеют стиль WS_MAXIMIZE (0x01000000) — они НЕ являются играми!
                    if ((style & WS_MAXIMIZE) == WS_MAXIMIZE)
                    {
                        return false;
                    }

                    RECT rc;
                    if (GetWindowRect(fg, out rc))
                    {
                        Screen scr = screen ?? Screen.PrimaryScreen;
                        // Окно покрывает весь физический экран монитора
                        if (rc.Left <= scr.Bounds.Left && rc.Top <= scr.Bounds.Top &&
                            rc.Right >= scr.Bounds.Right && rc.Bottom >= scr.Bounds.Bottom)
                        {
                            // Если у окна отсутствует заголовок WS_CAPTION и оно не WS_MAXIMIZE, это настоящая полноэкранная игра
                            if ((style & WS_CAPTION) != WS_CAPTION)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch {}
            return false;
        }

        private void OnPollTick(object sender, EventArgs e)
        {
            if (this.IsDisposed)
            {
                StopPollTimer();
                return;
            }
            if (!Config.SuperHubEnabled)
            {
                if (this.Visible) this.Visible = false;
                return;
            }

            // Проверяем актуальный физический экран маркера (при смене монитора или разрешения)
            Screen realScr = GetRealScreen();
            if (this.AttachedScreen == null || 
                !string.Equals(this.AttachedScreen.DeviceName, realScr.DeviceName, StringComparison.OrdinalIgnoreCase) ||
                !realScr.Bounds.Contains(this.Bounds.Location))
            {
                this.AttachedScreen = realScr;
                PositionCollapsed();
            }

            // Автоматическое скрытие и блокировка маркера, если запущена полноэкранная игра
            if (IsFullScreenGameOrAppActive(this.AttachedScreen))
            {
                if (this.Visible)
                {
                    this.Visible = false;
                }
                hoverExpandCounter = 0;
                return;
            }
            else
            {
                if (!this.Visible)
                {
                    this.Visible = true;
                }
            }

            DesktopHelper.POINT pt;
            if (!GetCursorPos(out pt)) return;

            // Во время активного Drag-and-Drop или удерживания ЛКМ не перебиваем фокус и не раскрываем окно
            if (ClipboardFlyoutForm.IsGlobalDraggingActive || ClipboardFlyoutForm.IsLeftMouseButtonDown())
            {
                hoverExpandCounter = 0;
                return;
            }

            Point cur = new Point(pt.x, pt.y);

            // Зона срабатывания строго у маркера: границы маркера + минимальный буфер 2px
            Rectangle hitArea = this.Bounds;
            hitArea.Inflate(2, 2);

            bool isOverUs = hitArea.Contains(cur);

            if (isOverUs)
            {
                isMouseHovering = true;
                HandleMarkerHover(cur);
            }
            else
            {
                if (hoveredHalf != 0 || isMouseHovering)
                {
                    hoveredHalf = 0;
                    isMouseHovering = false;
                    this.Invalidate();
                }
                hoverExpandCounter = 0;
            }
        }

        private void HandleMarkerHover(Point screenPt)
        {
            if (this.IsDisposed || !Config.SuperHubEnabled) return;
            if (ClipboardFlyoutForm.IsGlobalDraggingActive || ClipboardFlyoutForm.IsLeftMouseButtonDown()) return;

            int half = GetHalfForScreenPoint(screenPt);
            if (half != hoveredHalf)
            {
                hoveredHalf = half;
                this.Invalidate();
            }

            bool openSh = ShouldOpenSuperHubAtScreenPoint(screenPt);
            int targetTab = openSh ? 1 : 0;

            if (ClipboardFlyoutForm.IsFlyoutOpen)
            {
                hoverExpandCounter = 0;
                var flyout = ClipboardFlyoutForm.CurrentInstance;
                if (flyout != null && !flyout.IsDisposed && flyout.Visible)
                {
                    if (flyout.CurrentMainTab != targetTab)
                    {
                        flyout.SwitchMainTab(targetTab);
                    }
                }
            }
            else
            {
                hoverExpandCounter++;
                if (hoverExpandCounter >= 1) // Мгновенный отклик при наведении прямо на маркер
                {
                    hoverExpandCounter = 0;
                    ExpandShelf(false, openSh);
                }
            }
        }

        public void ExpandShelf(bool force = false, bool openSuperHub = true)
        {
            if (!Config.SuperHubEnabled) return;

            // Всегда используем актуальный физический экран маркера!
            Screen scr = GetRealScreen();
            this.AttachedScreen = scr;

            if (!force && IsFullScreenGameOrAppActive(scr)) return;
            try
            {
                IntPtr prevFg = Program.FindLastActiveUserWindow(scr);
                string pos = Config.GetScreenPosition(scr.DeviceName, Config.SuperHubPosition);
                ClipboardFlyoutForm.ShowDockFlyout(scr, pos, prevFg, openSuperHub, this.Bounds);
            }
            catch (Exception ex)
            {
                Logger.Log("SuperHubDockForm ExpandShelf error: " + ex.Message);
            }
        }

        public void CollapseShelf(bool animate = true)
        {
            // Маркер всегда в свернутом виде
        }

        public void RefreshItems()
        {
            this.Invalidate();
        }

        public bool IsPositionOnLeft()
        {
            string pos = Config.GetScreenPosition(this.AttachedScreen != null ? this.AttachedScreen.DeviceName : "", Config.SuperHubPosition);
            return pos.StartsWith("Left", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsHorizontalPosition()
        {
            string pos = Config.GetScreenPosition(this.AttachedScreen != null ? this.AttachedScreen.DeviceName : "", Config.SuperHubPosition);
            return string.Equals(pos, "TopCenter", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(pos, "BottomCenter", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsCornerPosition()
        {
            string pos = Config.GetScreenPosition(this.AttachedScreen != null ? this.AttachedScreen.DeviceName : "", Config.SuperHubPosition);
            return pos.IndexOf("Corner", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddLeftAnchoredCapsule(GraphicsPath path, int yStart, int segH, int w)
        {
            int r = Math.Min(w, segH / 2);
            path.StartFigure();
            path.AddLine(0, yStart + segH, 0, yStart);
            path.AddLine(0, yStart, Math.Max(0, w - r), yStart);
            path.AddArc(w - r * 2, yStart, r * 2, r * 2, 270, 90);
            path.AddLine(w, yStart + r, w, yStart + segH - r);
            path.AddArc(w - r * 2, yStart + segH - r * 2, r * 2, r * 2, 0, 90);
            path.AddLine(Math.Max(0, w - r), yStart + segH, 0, yStart + segH);
            path.CloseFigure();
        }

        private static void AddRightAnchoredCapsule(GraphicsPath path, int yStart, int segH, int w)
        {
            int r = Math.Min(w, segH / 2);
            path.StartFigure();
            path.AddLine(w, yStart, w, yStart + segH);
            path.AddLine(w, yStart + segH, r, yStart + segH);
            path.AddArc(0, yStart + segH - r * 2, r * 2, r * 2, 90, 90);
            path.AddLine(0, yStart + segH - r, 0, yStart + r);
            path.AddArc(0, yStart, r * 2, r * 2, 180, 90);
            path.AddLine(r, yStart, w, yStart);
            path.CloseFigure();
        }

        private static void AddTopAnchoredCapsule(GraphicsPath path, int xStart, int segW, int h)
        {
            int r = Math.Min(h, segW / 2);
            path.StartFigure();
            path.AddLine(xStart, 0, xStart + segW, 0);
            path.AddLine(xStart + segW, 0, xStart + segW, Math.Max(0, h - r));
            path.AddArc(xStart + segW - r * 2, h - r * 2, r * 2, r * 2, 0, 90);
            path.AddLine(xStart + segW - r, h, xStart + r, h);
            path.AddArc(xStart, h - r * 2, r * 2, r * 2, 90, 90);
            path.AddLine(xStart, Math.Max(0, h - r), xStart, 0);
            path.CloseFigure();
        }

        private static void AddBottomAnchoredCapsule(GraphicsPath path, int xStart, int segW, int h)
        {
            int r = Math.Min(h, segW / 2);
            path.StartFigure();
            path.AddLine(xStart + segW, h, xStart, h);
            path.AddLine(xStart, h, xStart, r);
            path.AddArc(xStart, 0, r * 2, r * 2, 180, 90);
            path.AddLine(xStart + r, 0, xStart + segW - r, 0);
            path.AddArc(xStart + segW - r * 2, 0, r * 2, r * 2, 270, 90);
            path.AddLine(xStart + segW, r, xStart + segW, h);
            path.CloseFigure();
        }

        public void PositionCollapsed()
        {
            if (!Config.SuperHubEnabled)
            {
                this.Visible = false;
                return;
            }

            Screen scr = this.AttachedScreen ?? Screen.PrimaryScreen;
            Rectangle work = scr.WorkingArea;
            string pos = Config.GetScreenPosition(scr.DeviceName, Config.SuperHubPosition);

            bool split = Config.SuperHubSplitMarker;
            int barThick = split ? SplitBarThickness : BarThickness;
            int barLen = split ? SplitBarLength : BarLength;
            int cornerRad = split ? SplitCornerRadius : CornerRadius;

            int x = 0, y = 0, w = barThick, h = barLen;

            if (string.Equals(pos, "CornerBottomRight", StringComparison.OrdinalIgnoreCase))
            {
                w = cornerRad;
                h = cornerRad;
                x = work.Right - w;
                y = work.Bottom - h;
            }
            else if (string.Equals(pos, "CornerBottomLeft", StringComparison.OrdinalIgnoreCase))
            {
                w = cornerRad;
                h = cornerRad;
                x = work.Left;
                y = work.Bottom - h;
            }
            else if (string.Equals(pos, "CornerTopRight", StringComparison.OrdinalIgnoreCase))
            {
                w = cornerRad;
                h = cornerRad;
                x = work.Right - w;
                y = work.Top;
            }
            else if (string.Equals(pos, "CornerTopLeft", StringComparison.OrdinalIgnoreCase))
            {
                w = cornerRad;
                h = cornerRad;
                x = work.Left;
                y = work.Top;
            }
            else if (string.Equals(pos, "TopCenter", StringComparison.OrdinalIgnoreCase))
            {
                w = barLen;
                h = barThick;
                x = work.Left + (work.Width - w) / 2;
                y = work.Top;
            }
            else if (string.Equals(pos, "BottomCenter", StringComparison.OrdinalIgnoreCase))
            {
                w = barLen;
                h = barThick;
                x = work.Left + (work.Width - w) / 2;
                y = work.Bottom - h;
            }
            else if (pos.StartsWith("Left", StringComparison.OrdinalIgnoreCase))
            {
                w = barThick;
                h = barLen;
                x = work.Left;
                if (pos.EndsWith("Top", StringComparison.OrdinalIgnoreCase)) y = work.Top + 24;
                else if (pos.EndsWith("Bottom", StringComparison.OrdinalIgnoreCase)) y = work.Bottom - h - 24;
                else y = work.Top + (work.Height - h) / 2;
            }
            else // Right (RightCenter, RightTop, RightBottom)
            {
                w = barThick;
                h = barLen;
                x = work.Right - w;
                if (pos.EndsWith("Top", StringComparison.OrdinalIgnoreCase)) y = work.Top + 24;
                else if (pos.EndsWith("Bottom", StringComparison.OrdinalIgnoreCase)) y = work.Bottom - h - 24;
                else y = work.Top + (work.Height - h) / 2;
            }

            this.SetBounds(x, y, w, h);

            // Создание скругленного региона маркера:
            // В режиме разделения (split) — две отдельные капсулы с зазором SplitGap посередине.
            try
            {
                using (var path = new GraphicsPath())
                {
                    if (IsCornerPosition())
                    {
                        if (string.Equals(pos, "CornerBottomRight", StringComparison.OrdinalIgnoreCase))
                        {
                            path.AddPie(0, 0, w * 2, h * 2, 180, 90);
                        }
                        else if (string.Equals(pos, "CornerBottomLeft", StringComparison.OrdinalIgnoreCase))
                        {
                            path.AddPie(-w, 0, w * 2, h * 2, 270, 90);
                        }
                        else if (string.Equals(pos, "CornerTopRight", StringComparison.OrdinalIgnoreCase))
                        {
                            path.AddPie(0, -h, w * 2, h * 2, 90, 90);
                        }
                        else // CornerTopLeft
                        {
                            path.AddPie(-w, -h, w * 2, h * 2, 0, 90);
                        }
                    }
                    else if (pos.StartsWith("Left", StringComparison.OrdinalIgnoreCase))
                    {
                        if (split)
                        {
                            int segH = (h - SplitGap) / 2;
                            AddLeftAnchoredCapsule(path, 0, segH, w);
                            AddLeftAnchoredCapsule(path, segH + SplitGap, h - (segH + SplitGap), w);
                        }
                        else
                        {
                            AddLeftAnchoredCapsule(path, 0, h, w);
                        }
                    }
                    else if (string.Equals(pos, "TopCenter", StringComparison.OrdinalIgnoreCase))
                    {
                        if (split)
                        {
                            int segW = (w - SplitGap) / 2;
                            AddTopAnchoredCapsule(path, 0, segW, h);
                            AddTopAnchoredCapsule(path, segW + SplitGap, w - (segW + SplitGap), h);
                        }
                        else
                        {
                            AddTopAnchoredCapsule(path, 0, w, h);
                        }
                    }
                    else if (string.Equals(pos, "BottomCenter", StringComparison.OrdinalIgnoreCase))
                    {
                        if (split)
                        {
                            int segW = (w - SplitGap) / 2;
                            AddBottomAnchoredCapsule(path, 0, segW, h);
                            AddBottomAnchoredCapsule(path, segW + SplitGap, w - (segW + SplitGap), h);
                        }
                        else
                        {
                            AddBottomAnchoredCapsule(path, 0, w, h);
                        }
                    }
                    else
                    {
                        if (split)
                        {
                            int segH = (h - SplitGap) / 2;
                            AddRightAnchoredCapsule(path, 0, segH, w);
                            AddRightAnchoredCapsule(path, segH + SplitGap, h - (segH + SplitGap), w);
                        }
                        else
                        {
                            AddRightAnchoredCapsule(path, 0, h, w);
                        }
                    }
                    this.Region = new Region(path);
                }
            }
            catch {}

            this.Visible = true;
            SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            this.BringToFront();
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            if (!Config.SuperHubSplitMarker)
            {
                Color markerColor = isMouseHovering ? ThemeHelper.AccentHover : ThemeHelper.Accent;
                using (var b = new SolidBrush(markerColor))
                {
                    e.Graphics.FillRectangle(b, 0, 0, this.Width, this.Height);
                }
                return;
            }

            // Двухсекционный режим:
            // 1-я половина (синяя) — Буфер обмена
            // 2-я половина (изумрудная) — Полка SuperHub
            Color firstColor = (isMouseHovering && hoveredHalf == 1) ? ThemeHelper.AccentHover : ThemeHelper.Accent;
            Color secondColor = (isMouseHovering && hoveredHalf == 2) ? ThemeHelper.SuperHubSecondaryAccentHover : ThemeHelper.SuperHubSecondaryAccent;

            if (IsCornerPosition())
            {
                string pos = Config.GetScreenPosition(this.AttachedScreen != null ? this.AttachedScreen.DeviceName : null, Config.SuperHubPosition);
                int w = this.Width;
                int h = this.Height;
                using (var b1 = new SolidBrush(firstColor))
                using (var b2 = new SolidBrush(secondColor))
                {
                    if (string.Equals(pos, "CornerBottomRight", StringComparison.OrdinalIgnoreCase))
                    {
                        // Центр в (w, h). Диагональ под 225°.
                        e.Graphics.FillPie(b1, 0, 0, w * 2, h * 2, 180, 45);
                        e.Graphics.FillPie(b2, 0, 0, w * 2, h * 2, 225, 45);
                    }
                    else if (string.Equals(pos, "CornerBottomLeft", StringComparison.OrdinalIgnoreCase))
                    {
                        // Центр в (0, h). Диагональ под 315°.
                        e.Graphics.FillPie(b1, -w, 0, w * 2, h * 2, 270, 45);
                        e.Graphics.FillPie(b2, -w, 0, w * 2, h * 2, 315, 45);
                    }
                    else if (string.Equals(pos, "CornerTopRight", StringComparison.OrdinalIgnoreCase))
                    {
                        // Центр в (w, 0). Диагональ под 135°.
                        e.Graphics.FillPie(b1, 0, -h, w * 2, h * 2, 90, 45);
                        e.Graphics.FillPie(b2, 0, -h, w * 2, h * 2, 135, 45);
                    }
                    else // CornerTopLeft
                    {
                        // Центр в (0, 0). Диагональ под 45°.
                        e.Graphics.FillPie(b1, -w, -h, w * 2, h * 2, 0, 45);
                        e.Graphics.FillPie(b2, -w, -h, w * 2, h * 2, 45, 45);
                    }
                }
            }
            else if (IsHorizontalPosition())
            {
                int midX = this.Width / 2;
                using (var b1 = new SolidBrush(firstColor))
                {
                    e.Graphics.FillRectangle(b1, 0, 0, midX, this.Height);
                }
                using (var b2 = new SolidBrush(secondColor))
                {
                    e.Graphics.FillRectangle(b2, midX, 0, this.Width - midX, this.Height);
                }
            }
            else
            {
                int midY = this.Height / 2;
                using (var b1 = new SolidBrush(firstColor))
                {
                    e.Graphics.FillRectangle(b1, 0, 0, this.Width, midY);
                }
                using (var b2 = new SolidBrush(secondColor))
                {
                    e.Graphics.FillRectangle(b2, 0, midY, this.Width, this.Height - midY);
                }
            }
        }
    }
}
