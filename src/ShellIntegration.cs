using System;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PasteImageAsFile
{
    public static class ShellIntegration
    {
        public const string AppName = "ClipShelf";
        public const string LegacyAppName = "PasteImageAsFile";
        public const string MenuText = "Вставить изображение (ClipShelf)";
        public const string HistoryMenuText = "ClipShelf Буфер обмена";
        public const string AppVersion = "1.8.0";

        [DllImport("shell32.dll")]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        const uint SHCNE_ASSOCCHANGED = 0x08000000;
        const uint SHCNF_IDLIST = 0x0000;

        public static string InstallDir
        {
            get
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localApp, "Programs", AppName);
            }
        }

        public static string InstalledExePath
        {
            get { return Path.Combine(InstallDir, AppName + ".exe"); }
        }

        public static bool IsInstalled
        {
            get { return File.Exists(InstalledExePath) && (IsAutoRunRegistered || IsContextMenuRegistered); }
        }

        public static bool IsAutoRunRegistered
        {
            get
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                    {
                        if (key == null) return false;
                        return key.GetValue(AppName) != null || key.GetValue(LegacyAppName) != null;
                    }
                }
                catch { return false; }
            }
        }

        public static bool IsContextMenuRegistered
        {
            get
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\Background\shell\ClipShelf", false))
                    {
                        if (key != null) return true;
                    }
                    using (var key2 = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\Background\shell\ClipShelf_History", false))
                    {
                        if (key2 != null) return true;
                    }
                    using (var key3 = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\Background\shell\PasteImageAsFile", false))
                    {
                        if (key3 != null) return true;
                    }
                    using (var key4 = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Directory\Background\shell\PasteImageAsFile_History", false))
                    {
                        return key4 != null;
                    }
                }
                catch { return false; }
            }
        }

        public static bool IsDaemonRunning()
        {
            int currentId = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcessesByName(AppName))
            {
                if (p.Id != currentId) return true;
            }
            foreach (var p in Process.GetProcessesByName(LegacyAppName))
            {
                if (p.Id != currentId) return true;
            }
            return false;
        }

        public static void Install(bool autoRun, bool contextMenu)
        {
            Directory.CreateDirectory(InstallDir);
            string currentExe = Application.ExecutablePath;

            if (!string.Equals(currentExe, InstalledExePath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(currentExe, InstalledExePath, true);
            }

            string currentDir = Path.GetDirectoryName(currentExe);
            string currentIco = Path.Combine(currentDir, "app.ico");
            string installedIco = Path.Combine(InstallDir, "app.ico");
            if (File.Exists(currentIco) && !string.Equals(currentIco, installedIco, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Copy(currentIco, installedIco, true); } catch {}
            }

            string currentPng = Path.Combine(currentDir, "app_icon.png");
            string installedPng = Path.Combine(InstallDir, "app_icon.png");
            if (File.Exists(currentPng) && !string.Equals(currentPng, installedPng, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Copy(currentPng, installedPng, true); } catch {}
            }

            SetContextMenu(contextMenu);
            SetAutoRun(autoRun);

            Config.AutoRun = autoRun;
            Config.ContextMenu = contextMenu;

            RefreshShellIcons();
        }

        public static void Uninstall()
        {
            StopDaemon();

            SetContextMenu(false);
            SetAutoRun(false);

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software", true))
                {
                    if (key != null)
                    {
                        key.DeleteSubKeyTree("ClipShelf", false);
                        key.DeleteSubKeyTree("PasteImageAsFile", false);
                    }
                }
            }
            catch {}

            RefreshShellIcons();
        }

        private static readonly string[] MenuLocations = new string[]
        {
            @"Software\Classes\Directory\Background\shell",
            @"Software\Classes\DesktopBackground\shell",
            @"Software\Classes\Drive\Background\shell"
        };

        public static void SetImagePasteMenuItem(bool visible)
        {
            if (!Config.ContextMenu)
            {
                foreach (var loc in MenuLocations)
                {
                    try { Registry.CurrentUser.DeleteSubKeyTree(loc + @"\ClipShelf", false); } catch {}
                    try { Registry.CurrentUser.DeleteSubKeyTree(loc + @"\PasteImageAsFile", false); } catch {}
                }
                return;
            }

            string exePath = File.Exists(InstalledExePath) ? InstalledExePath : Application.ExecutablePath;
            string quotedExe = "\"" + exePath + "\"";
            string iconPath = Path.Combine(Path.GetDirectoryName(exePath), "app.ico");
            string iconRef = File.Exists(iconPath) ? ("\"" + iconPath + "\",0") : ("\"" + exePath + "\",0");

            foreach (var loc in MenuLocations)
            {
                // Clean up legacy key
                try { Registry.CurrentUser.DeleteSubKeyTree(loc + @"\PasteImageAsFile", false); } catch {}

                string keyPath = loc + @"\ClipShelf";
                if (visible)
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
                    {
                        if (key != null)
                        {
                            key.SetValue("", MenuText);
                            key.SetValue("Icon", iconRef);
                            using (var cmd = key.CreateSubKey("command"))
                            {
                                if (cmd != null) cmd.SetValue("", quotedExe + " --save \"%V\"");
                            }
                        }
                    }
                }
                else
                {
                    try { Registry.CurrentUser.DeleteSubKeyTree(keyPath, false); } catch {}
                }
            }
        }

        public static void SetHistoryMenuItem(bool visible)
        {
            foreach (var loc in MenuLocations)
            {
                // Clean up legacy key
                try { Registry.CurrentUser.DeleteSubKeyTree(loc + @"\PasteImageAsFile_History", false); } catch {}

                string keyPath = loc + @"\ClipShelf_History";
                if (!Config.ContextMenu || !visible)
                {
                    try { Registry.CurrentUser.DeleteSubKeyTree(keyPath, false); } catch {}
                }
                else
                {
                    string exePath = File.Exists(InstalledExePath) ? InstalledExePath : Application.ExecutablePath;
                    string quotedExe = "\"" + exePath + "\"";
                    string iconPath = Path.Combine(Path.GetDirectoryName(exePath), "app.ico");
                    string iconRef = File.Exists(iconPath) ? ("\"" + iconPath + "\",0") : ("\"" + exePath + "\",0");

                    using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
                    {
                        if (key != null)
                        {
                            key.SetValue("", HistoryMenuText);
                            key.SetValue("Icon", iconRef);
                            using (var cmd = key.CreateSubKey("command"))
                            {
                                if (cmd != null) cmd.SetValue("", quotedExe + " --history");
                            }
                        }
                    }
                }
            }
        }

        public static void SetContextMenu(bool enable)
        {
            if (enable)
            {
                SetHistoryMenuItem(Config.ShowHistoryMenu);
                bool hasCached = HasAnyCachedImage();
                SetImagePasteMenuItem(hasCached);
            }
            else
            {
                SetImagePasteMenuItem(false);
                SetHistoryMenuItem(false);
            }

            RefreshShellIcons();
        }

        public static bool IsClassicContextMenuWin11Enabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", false))
                {
                    return key != null;
                }
            }
            catch { return false; }
        }

        public static void SetClassicContextMenuWin11(bool enable)
        {
            try
            {
                if (enable)
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32"))
                    {
                        if (key != null) key.SetValue("", "");
                    }
                }
                else
                {
                    try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}", false); } catch {}
                }
                RefreshShellIcons();
            }
            catch {}
        }

        public static bool HasAnyCachedImage()
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string cacheDir = Path.Combine(localApp, AppName, "Cache");
                if (Directory.Exists(cacheDir))
                {
                    return Directory.GetFiles(cacheDir, "*.png").Length > 0;
                }
            }
            catch {}
            return false;
        }

        public static void SetAutoRun(bool enable)
        {
            string exePath = File.Exists(InstalledExePath) ? InstalledExePath : Application.ExecutablePath;
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            key.SetValue(AppName, "\"" + exePath + "\" --daemon");
                        }
                        else
                        {
                            key.DeleteValue(AppName, false);
                        }
                    }
                }
            }
            catch {}
        }

        public static void RefreshShellIcons()
        {
            try
            {
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            }
            catch {}
        }

        public static void StopDaemon()
        {
            int currentId = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcessesByName(AppName))
            {
                if (p.Id != currentId)
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(1000);
                    }
                    catch {}
                }
            }
            foreach (var p in Process.GetProcessesByName(LegacyAppName))
            {
                if (p.Id != currentId)
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(1000);
                    }
                    catch {}
                }
            }
        }

        public static void StartDaemon()
        {
            if (IsDaemonRunning()) return;

            string target = File.Exists(InstalledExePath) ? InstalledExePath : Application.ExecutablePath;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = target,
                    Arguments = "--daemon",
                    UseShellExecute = true
                });
            }
            catch {}
        }

        public static void RestartDaemon()
        {
            StopDaemon();
            StartDaemon();
        }
    }
}
