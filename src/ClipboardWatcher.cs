using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Threading;

namespace PasteImageAsFile
{
    public class ClipboardListenerWindow : NativeWindow
    {
        public static ClipboardListenerWindow Instance { get; private set; }

        const int WM_CLIPBOARDUPDATE = 0x031D;
        const int WM_HOTKEY = 0x0312;
        public const int WM_SHOW_FLYOUT = 0x042A;
        public const int WM_SHOW_SUPERHUB = 0x042B;

        private const int HOTKEY_ID_CLIPBOARD = 9001;
        private const int HOTKEY_ID_SUPERHUB_ADD = 9002;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_V = 0x56;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_MENU = 0x12;
        private const byte VK_SHIFT_KEY = 0x10;
        private const byte VK_C = 0x43;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc keyboardProcDelegate;
        private IntPtr keyboardHookHandle = IntPtr.Zero;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool SetWindowText(IntPtr hWnd, string lpString);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint RegisterWindowMessage(string lpString);

        public const string FLYOUT_MSG_NAME = "ClipShelf_ShowFlyout_Message_v1";
        public static readonly uint WM_SHOW_FLYOUT_MSG = RegisterWindowMessage(FLYOUT_MSG_NAME);
        public static readonly uint WM_SHOW_FLYOUT_MSG_LEGACY = RegisterWindowMessage("PasteImageAsFile_ShowFlyout_Message_v1");

        public const string SUPERHUB_MSG_NAME = "ClipShelf_ShowSuperHub_Message_v1";
        public static readonly uint WM_SHOW_SUPERHUB_MSG = RegisterWindowMessage(SUPERHUB_MSG_NAME);
        public static readonly uint WM_SHOW_SUPERHUB_MSG_LEGACY = RegisterWindowMessage("PasteImageAsFile_ShowSuperHub_Message_v1");

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public const string FlyoutEventName = "ClipShelf_ShowFlyout_Event_v1";
        private EventWaitHandle flyoutEvent;
        private RegisteredWaitHandle registeredWait;

        public const string SuperHubEventName = "ClipShelf_ShowSuperHub_Event_v1";
        private EventWaitHandle superHubEvent;
        private RegisteredWaitHandle registeredSuperHubWait;

        public string CacheDirectory { get; private set; }
        private string lastAugmentedFile = null;
        private DateTime lastAugmentTime = DateTime.MinValue;
        private const int DebounceMs = 500;
        private FileSystemWatcher desktopWatcher;

        public ClipboardListenerWindow()
        {
            Instance = this;
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            CacheDirectory = Path.Combine(localApp, ShellIntegration.AppName, "Cache");
            Directory.CreateDirectory(CacheDirectory);

            CleanOldCache();

            CreateParams cp = new CreateParams();
            cp.Caption = "ClipShelfListenerWindow";
            cp.Style = 0;
            cp.Parent = IntPtr.Zero;
            this.CreateHandle(cp);
            SetWindowText(this.Handle, "ClipShelfListenerWindow");
            bool ok = AddClipboardFormatListener(this.Handle);
            Logger.Log("ClipboardListenerWindow handle created: " + this.Handle + ", listener added: " + ok);

            try
            {
                bool createdNew;
                flyoutEvent = new EventWaitHandle(false, EventResetMode.AutoReset, FlyoutEventName, out createdNew);
                registeredWait = ThreadPool.RegisterWaitForSingleObject(flyoutEvent, (state, timedOut) => {
                    if (!timedOut)
                    {
                        PostMessage(this.Handle, unchecked((uint)WM_SHOW_FLYOUT), IntPtr.Zero, IntPtr.Zero);
                    }
                }, null, -1, false);

                bool createdSuperHub;
                superHubEvent = new EventWaitHandle(false, EventResetMode.AutoReset, SuperHubEventName, out createdSuperHub);
                registeredSuperHubWait = ThreadPool.RegisterWaitForSingleObject(superHubEvent, (state, timedOut) => {
                    if (!timedOut)
                    {
                        PostMessage(this.Handle, unchecked((uint)WM_SHOW_SUPERHUB), IntPtr.Zero, IntPtr.Zero);
                    }
                }, null, -1, false);
            }
            catch (Exception ex)
            {
                Logger.Log("Error initializing IPC events: " + ex.Message);
            }

            InitDesktopWatcher();
            ApplyGlobalHotkeys();

            // Проверяем начальное состояние буфера для меню и захватываем текущий текст/файлы
            try
            {
                if (!Clipboard.ContainsImage())
                {
                    if (Clipboard.ContainsText() && Config.HistoryRememberText)
                    {
                        string initTxt = Clipboard.GetText();
                        if (!string.IsNullOrEmpty(initTxt) && initTxt.Trim().Length > 0)
                        {
                            ClipboardHistoryManager.Instance.AddText(initTxt, "");
                        }
                    }
                }
                UpdateMenuStateForClipboard();
            }
            catch {}
        }

        public static void ReloadHotkeys()
        {
            Program.PostToUi(delegate {
                if (Instance != null)
                {
                    Instance.ApplyGlobalHotkeys();
                }
            });
        }

        public static void ReloadAllHotkeys()
        {
            ReloadHotkeys();
        }

        public void ApplyGlobalHotkeys()
        {
            if (this.Handle == IntPtr.Zero) return;

            try { UnregisterHotKey(this.Handle, HOTKEY_ID_CLIPBOARD); } catch {}
            try { UnregisterHotKey(this.Handle, HOTKEY_ID_SUPERHUB_ADD); } catch {}

            if (keyboardHookHandle != IntPtr.Zero)
            {
                try { UnhookWindowsHookEx(keyboardHookHandle); } catch {}
                keyboardHookHandle = IntPtr.Zero;
            }

            // Устанавливаем низкоуровневый хук для перехвата Win+V и закрытия окна по Escape без потери фокуса ввода
            try
            {
                if (keyboardProcDelegate == null)
                {
                    keyboardProcDelegate = KeyboardHookCallback;
                }
                using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    keyboardHookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardProcDelegate, GetModuleHandle(curModule.ModuleName), 0);
                }
                Logger.Log("Installed WH_KEYBOARD_LL hook: " + (keyboardHookHandle != IntPtr.Zero));
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to set keyboard hook: " + ex.Message);
            }

            // 1. Горячая клавиша вызова буфера обмена
            string cbHk = Config.ClipboardHotkey ?? "Win+V";
            if (string.Equals(cbHk, "Ctrl+Shift+V", StringComparison.OrdinalIgnoreCase))
            {
                RegisterHotKey(this.Handle, HOTKEY_ID_CLIPBOARD, MOD_CONTROL | MOD_SHIFT, (uint)Keys.V);
            }
            else if (string.Equals(cbHk, "Alt+V", StringComparison.OrdinalIgnoreCase))
            {
                RegisterHotKey(this.Handle, HOTKEY_ID_CLIPBOARD, MOD_ALT, (uint)Keys.V);
            }
            else if (string.Equals(cbHk, "Ctrl+~", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(cbHk, "Ctrl+Tilde", StringComparison.OrdinalIgnoreCase))
            {
                RegisterHotKey(this.Handle, HOTKEY_ID_CLIPBOARD, MOD_CONTROL, (uint)Keys.Oemtilde);
            }

            // 2. Горячая клавиша быстрого добавления в SuperHub
            string shHk = Config.SuperHubAddHotkey ?? "Ctrl+Alt+C";
            if (string.Equals(shHk, "Ctrl+Alt+C", StringComparison.OrdinalIgnoreCase))
            {
                RegisterHotKey(this.Handle, HOTKEY_ID_SUPERHUB_ADD, MOD_CONTROL | MOD_ALT, (uint)Keys.C);
            }
            else if (string.Equals(shHk, "Ctrl+Shift+C", StringComparison.OrdinalIgnoreCase))
            {
                RegisterHotKey(this.Handle, HOTKEY_ID_SUPERHUB_ADD, MOD_CONTROL | MOD_SHIFT, (uint)Keys.C);
            }
            else if (string.Equals(shHk, "Alt+C", StringComparison.OrdinalIgnoreCase))
            {
                RegisterHotKey(this.Handle, HOTKEY_ID_SUPERHUB_ADD, MOD_ALT, (uint)Keys.C);
            }
            else if (string.Equals(shHk, "Alt+S", StringComparison.OrdinalIgnoreCase))
            {
                RegisterHotKey(this.Handle, HOTKEY_ID_SUPERHUB_ADD, MOD_ALT, (uint)Keys.S);
            }
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (vkCode == 0x1B) // VK_ESCAPE
                {
                    if (ClipboardFlyoutForm.IsFlyoutOpen)
                    {
                        Program.PostToUi(delegate {
                            ClipboardFlyoutForm.CloseActiveFlyout();
                        });
                        return (IntPtr)1;
                    }
                }
                else if (vkCode == VK_V)
                {
                    bool winDown = ((GetAsyncKeyState(VK_LWIN) & 0x8000) != 0) || ((GetAsyncKeyState(VK_RWIN) & 0x8000) != 0);
                    if (winDown && string.Equals(Config.ClipboardHotkey, "Win+V", StringComparison.OrdinalIgnoreCase))
                    {
                        // Подавляем открытие меню Пуск при отпускании клавиши Win и вызываем наш буфер обмена
                        try
                        {
                            keybd_event(0xFF, 0, 0, UIntPtr.Zero);
                            keybd_event(0xFF, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                            PostMessage(this.Handle, unchecked((uint)WM_SHOW_FLYOUT), IntPtr.Zero, IntPtr.Zero);
                        }
                        catch {}
                        return (IntPtr)1;
                    }
                }
            }
            return CallNextHookEx(keyboardHookHandle, nCode, wParam, lParam);
        }

        private void HandleQuickAddToSuperHub()
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                // Отпускаем модификаторы Alt/Shift и отправляем Ctrl+C активному окну, чтобы захватить выделение
                keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_SHIFT_KEY, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                keybd_event(VK_C, 0, 0, UIntPtr.Zero);
                keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                System.Windows.Forms.Timer delayTimer = new System.Windows.Forms.Timer();
                delayTimer.Interval = 150;
                delayTimer.Tick += delegate(object s, EventArgs e) {
                    delayTimer.Stop();
                    delayTimer.Dispose();
                    try
                    {
                        ClipboardHistoryManager.Instance.PromoteCurrentOrLatestToSuperHub();
                        DesktopHelper.POINT curPt;
                        DesktopHelper.TryGetCursorPosition(out curPt);
                        Screen scr = Screen.FromPoint(new Point(curPt.x, curPt.y));
                        string pos = Config.GetScreenPosition(scr.DeviceName, Config.SuperHubPosition);
                        ClipboardFlyoutForm.ShowDockFlyout(scr, pos, fg, true);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("HandleQuickAddToSuperHub delayed error: " + ex.Message);
                    }
                };
                delayTimer.Start();
            }
            catch (Exception ex)
            {
                Logger.Log("HandleQuickAddToSuperHub error: " + ex.Message);
            }
        }

        private void InitDesktopWatcher()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (Directory.Exists(desktopPath))
                {
                    desktopWatcher = new FileSystemWatcher(desktopPath);
                    desktopWatcher.Filter = "*.*";
                    desktopWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime;
                    desktopWatcher.Created += OnDesktopFileCreated;
                    desktopWatcher.EnableRaisingEvents = true;
                    Logger.Log("Desktop FileSystemWatcher initialized on " + desktopPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("InitDesktopWatcher error: " + ex.Message);
            }
        }

        private void OnDesktopFileCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (!Config.PlaceUnderCursor) return;

                string ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".bmp") return;

                double elapsed = (DateTime.UtcNow - lastAugmentTime).TotalSeconds;
                bool isRecent = elapsed < 60.0;
                bool isOurFile = !string.IsNullOrEmpty(lastAugmentedFile) &&
                                 string.Equals(Path.GetFileNameWithoutExtension(e.FullPath), Path.GetFileNameWithoutExtension(lastAugmentedFile), StringComparison.OrdinalIgnoreCase);

                if (isRecent || isOurFile)
                {
                    Logger.Log("Desktop file created from paste detected: " + e.FullPath);
                    Thread.Sleep(120);
                    DesktopHelper.PositionFile(e.FullPath, true);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("OnDesktopFileCreated error: " + ex.Message);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int hkId = m.WParam.ToInt32();
                if (hkId == HOTKEY_ID_CLIPBOARD)
                {
                    PostMessage(this.Handle, unchecked((uint)WM_SHOW_FLYOUT), IntPtr.Zero, IntPtr.Zero);
                    return;
                }
                else if (hkId == HOTKEY_ID_SUPERHUB_ADD)
                {
                    HandleQuickAddToSuperHub();
                    return;
                }
            }

            if (m.Msg == (int)WM_SHOW_FLYOUT_MSG || m.Msg == (int)WM_SHOW_FLYOUT_MSG_LEGACY || m.Msg == WM_SHOW_FLYOUT)
            {
                Logger.Log("WM_SHOW_FLYOUT message received");
                try
                {
                    IntPtr prevFg = GetForegroundWindow();
                    DesktopHelper.POINT curPt;
                    DesktopHelper.TryGetCursorPosition(out curPt);
                    Point pt = new Point(curPt.x, curPt.y);
                    Screen scr = Screen.FromPoint(pt);
                    if (prevFg == IntPtr.Zero || Program.IsTaskbarOrTrayWindow(prevFg))
                    {
                        IntPtr userWnd = Program.FindLastActiveUserWindow(scr);
                        if (userWnd != IntPtr.Zero) prevFg = userWnd;
                    }
                    ClipboardFlyoutForm.ShowFlyout(pt, prevFg);
                }
                catch (Exception ex)
                {
                    Logger.Log("Error showing flyout from WM_SHOW_FLYOUT: " + ex.Message);
                }
                return;
            }

            if (m.Msg == (int)WM_SHOW_SUPERHUB_MSG || m.Msg == (int)WM_SHOW_SUPERHUB_MSG_LEGACY || m.Msg == WM_SHOW_SUPERHUB)
            {
                Logger.Log("WM_SHOW_SUPERHUB message received");
                try
                {
                    IntPtr prevFg = GetForegroundWindow();
                    DesktopHelper.POINT curPt;
                    DesktopHelper.TryGetCursorPosition(out curPt);
                    Point pt = new Point(curPt.x, curPt.y);
                    Screen scr = Screen.FromPoint(pt);
                    if (prevFg == IntPtr.Zero || Program.IsTaskbarOrTrayWindow(prevFg))
                    {
                        IntPtr userWnd = Program.FindLastActiveUserWindow(scr);
                        if (userWnd != IntPtr.Zero) prevFg = userWnd;
                    }
                    string pos = Config.GetScreenPosition(scr.DeviceName, Config.SuperHubPosition);
                    ClipboardFlyoutForm.ShowDockFlyout(scr, pos, prevFg, true);
                }
                catch (Exception ex)
                {
                    Logger.Log("Error showing superhub from WM_SHOW_SUPERHUB: " + ex.Message);
                }
                return;
            }

            const int WM_DISPLAYCHANGE = 0x007E;
            const int WM_SETTINGCHANGE = 0x001A;

            if (m.Msg == WM_DISPLAYCHANGE || m.Msg == WM_SETTINGCHANGE)
            {
                Logger.Log("Display settings or screen change message received (0x" + m.Msg.ToString("X") + ")");
                SuperHubDockForm.ScheduleSyncAllDocks(250);
            }

            if (m.Msg == WM_CLIPBOARDUPDATE)
            {
                // Debounce: подавление повторных событий после нашей же записи в буфер
                double elapsed = (DateTime.UtcNow - lastAugmentTime).TotalMilliseconds;
                if (elapsed < DebounceMs)
                {
                    Logger.Log("WM_CLIPBOARDUPDATE debounced (" + (int)elapsed + "ms)");
                    base.WndProc(ref m);
                    return;
                }
                Logger.Log("WM_CLIPBOARDUPDATE received");
                OnClipboardUpdated();
            }
            base.WndProc(ref m);
        }

        private void UpdateMenuStateForClipboard()
        {
            try
            {
                bool hasImage = false;
                if (Clipboard.ContainsImage())
                {
                    hasImage = true;
                }
                else if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    if (files.Count == 1 && File.Exists(files[0]))
                    {
                        string ext = Path.GetExtension(files[0]).ToLowerInvariant();
                        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                        {
                            hasImage = true;
                        }
                    }
                }

                if (hasImage)
                {
                    // В буфере сейчас картинка -> работает стандартный Ctrl+V, пункт меню скрываем
                    ShellIntegration.SetImagePasteMenuItem(false);
                }
                else
                {
                    // В буфере сейчас НЕ картинка -> если в кэше есть сохраненная картинка, показываем пункт
                    bool hasCached = ShellIntegration.HasAnyCachedImage();
                    ShellIntegration.SetImagePasteMenuItem(hasCached);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("UpdateMenuStateForClipboard error: " + ex.Message);
            }
        }

        private void OnClipboardUpdated()
        {
            for (int r = 0; r < 5; r++)
            {
                try
                {
                    // 1. Проверяем наличие изображения в буфере
                    if (Clipboard.ContainsImage())
                    {
                        HandleImageInClipboard();
                        return;
                    }

                    // 2. Проверяем наличие скопированных файлов/папок
                    if (Clipboard.ContainsFileDropList() && Config.HistoryRememberFiles)
                    {
                        var files = Clipboard.GetFileDropList();
                        if (files != null && files.Count > 0)
                        {
                            if (files.Count == 1 && string.Equals(files[0], lastAugmentedFile, StringComparison.OrdinalIgnoreCase))
                            {
                                return;
                            }

                            string winTitle = NameHelper.GetActiveWindowTitle() ?? "Проводник";
                            var fileList = new List<string>();
                            foreach (string f in files) fileList.Add(f);

                            ClipboardHistoryManager.Instance.AddFiles(fileList, winTitle);
                            Logger.Log("History: added " + fileList.Count + " files from " + winTitle);
                            UpdateMenuStateForClipboard();
                            return;
                        }
                    }

                    // 3. Проверяем наличие текста
                    if (Clipboard.ContainsText() && Config.HistoryRememberText)
                    {
                        string text = Clipboard.GetText();
                        if (!string.IsNullOrEmpty(text) && text.Trim().Length > 0)
                        {
                            string winTitle = NameHelper.GetActiveWindowTitle() ?? "";
                            ClipboardHistoryManager.Instance.AddText(text, winTitle);
                            Logger.Log("History: added text snippet (" + text.Length + " chars) from " + winTitle);
                            UpdateMenuStateForClipboard();
                            return;
                        }
                    }

                    UpdateMenuStateForClipboard();
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Log("OnClipboardUpdated retry " + r + ": " + ex.Message);
                    Thread.Sleep(35);
                }
            }
        }

        private void HandleImageInClipboard()
        {
            Image img = null;
            IDataObject origData = null;

            for (int r = 0; r < 5; r++)
            {
                try
                {
                    if (Clipboard.ContainsFileDropList())
                    {
                        var files = Clipboard.GetFileDropList();
                        if (files.Count == 1 && string.Equals(files[0], lastAugmentedFile, StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }
                    }

                    origData = Clipboard.GetDataObject();
                    img = Clipboard.GetImage();
                    if (img != null && origData != null) break;
                }
                catch (Exception ex)
                {
                    Logger.Log("Read clipboard retry " + r + ": " + ex.Message);
                    Thread.Sleep(50);
                }
            }

            if (img == null || origData == null)
            {
                UpdateMenuStateForClipboard();
                return;
            }

            try
            {
                string bestName = NameHelper.GetBestImageName(origData);
                string ext = ".png";
                string tempFilePath = Path.Combine(CacheDirectory, bestName + ext);
                int count = 1;
                while (File.Exists(tempFilePath))
                {
                    tempFilePath = Path.Combine(CacheDirectory, bestName + " (" + count + ")" + ext);
                    count++;
                }

                img.Save(tempFilePath, ImageFormat.Png);
                long fSize = 0;
                try { fSize = new FileInfo(tempFilePath).Length; } catch {}
                Logger.Log("Saved cache image: " + tempFilePath);

                // Добавляем в менеджер истории
                ClipboardHistoryManager.Instance.AddImage(tempFilePath, bestName, img.Width, img.Height, fSize);

                DataObject aug = new DataObject();
                foreach (string fmt in origData.GetFormats())
                {
                    try
                    {
                        object val = origData.GetData(fmt);
                        if (val != null) aug.SetData(fmt, val);
                    }
                    catch {}
                }

                try { aug.SetImage(img); } catch {}

                StringCollection fl = new StringCollection();
                fl.Add(tempFilePath);
                aug.SetFileDropList(fl);

                byte[] dropEffect = new byte[] { 1, 0, 0, 0 };
                aug.SetData("Preferred DropEffect", new MemoryStream(dropEffect));

                lastAugmentedFile = tempFilePath;
                lastAugmentTime = DateTime.UtcNow;
                Clipboard.SetDataObject(aug, true);
                Logger.Log("Augmented clipboard with: " + tempFilePath);

                ShellIntegration.SetImagePasteMenuItem(false);
            }
            catch (Exception ex)
            {
                Logger.Log("Augmentation error: " + ex.Message);
            }
            finally
            {
                try { img.Dispose(); } catch {}
            }
        }

        public static void AugmentClipboardWithImage(string filePath)
        {
            if (!File.Exists(filePath)) return;
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var img = Image.FromStream(stream))
                {
                    DataObject data = new DataObject();
                    data.SetImage(new Bitmap(img));

                    StringCollection fl = new StringCollection();
                    fl.Add(filePath);
                    data.SetFileDropList(fl);

                    byte[] dropEffect = new byte[] { 1, 0, 0, 0 };
                    data.SetData("Preferred DropEffect", new MemoryStream(dropEffect));

                    if (Instance != null)
                    {
                        Instance.lastAugmentedFile = filePath;
                        Instance.lastAugmentTime = DateTime.UtcNow;
                    }
                    Clipboard.SetDataObject(data, true);
                    Logger.Log("AugmentClipboardWithImage: set file and image to clipboard: " + filePath);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("AugmentClipboardWithImage error: " + ex.Message);
            }
        }

        private void CleanOldCache()
        {
            try
            {
                var dir = new DirectoryInfo(CacheDirectory);
                var files = dir.GetFiles("*.png");
                if (files.Length > 50)
                {
                    Array.Sort(files, (a, b) => a.CreationTime.CompareTo(b.CreationTime));
                    for (int i = 0; i < files.Length - 50; i++)
                    {
                        try { files[i].Delete(); } catch {}
                    }
                }
            }
            catch {}
        }

        public void Destroy()
        {
            if (desktopWatcher != null)
            {
                try
                {
                    desktopWatcher.EnableRaisingEvents = false;
                    desktopWatcher.Dispose();
                    desktopWatcher = null;
                }
                catch {}
            }
            if (registeredWait != null)
            {
                try { registeredWait.Unregister(null); } catch {}
                registeredWait = null;
            }
            if (flyoutEvent != null)
            {
                try { flyoutEvent.Close(); } catch {}
                flyoutEvent = null;
            }
            if (registeredSuperHubWait != null)
            {
                try { registeredSuperHubWait.Unregister(null); } catch {}
                registeredSuperHubWait = null;
            }
            if (superHubEvent != null)
            {
                try { superHubEvent.Close(); } catch {}
                superHubEvent = null;
            }
            try { UnregisterHotKey(this.Handle, HOTKEY_ID_CLIPBOARD); } catch {}
            try { UnregisterHotKey(this.Handle, HOTKEY_ID_SUPERHUB_ADD); } catch {}
            if (keyboardHookHandle != IntPtr.Zero)
            {
                try { UnhookWindowsHookEx(keyboardHookHandle); } catch {}
                keyboardHookHandle = IntPtr.Zero;
            }
            RemoveClipboardFormatListener(this.Handle);
            this.DestroyHandle();
            Logger.Log("ClipboardListenerWindow destroyed");
        }
    }
}
