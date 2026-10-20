using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace PasteImageAsFile
{
    public static class FileIconHelper
    {
        private static readonly Dictionary<string, Image> cache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private static readonly object cacheLock = new object();

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Image GetIconForPath(string path, bool isLarge = false)
        {
            if (string.IsNullOrEmpty(path)) return SystemIcons.Application.ToBitmap();

            string ext = "";
            try { ext = Path.GetExtension(path).ToLowerInvariant(); } catch {}

            bool isUnc = path.StartsWith(@"\\") || path.StartsWith("//");
            bool isDir = false;

            if (string.IsNullOrEmpty(ext))
            {
                if (path.EndsWith("\\") || path.EndsWith("/") || (!isUnc && SafeDirectoryCheck(path)))
                {
                    isDir = true;
                }
            }

            string key = isDir ? ":folder:" : (string.IsNullOrEmpty(ext) ? ":file:" : ext);

            // Для уникальных локальных exe/ico файлов кэшируем по полному пути
            if (!isDir && (ext == ".exe" || ext == ".ico") && !isUnc)
            {
                key = path;
            }

            lock (cacheLock)
            {
                Image cached;
                if (cache.TryGetValue(key, out cached))
                {
                    if (cached != null) return cached;
                    cache.Remove(key);
                }
            }

            Image img = ExtractIcon(path, ext, isDir, isUnc);
            if (img == null) img = SystemIcons.Application.ToBitmap();

            lock (cacheLock)
            {
                cache[key] = img;
            }

            return img;
        }

        private static bool SafeDirectoryCheck(string path)
        {
            try
            {
                if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return false;
                return Directory.Exists(path);
            }
            catch { return false; }
        }

        private static Image ExtractIcon(string path, string ext, bool isDir, bool isUnc)
        {
            try
            {
                SHFILEINFO shinfo = new SHFILEINFO();
                uint flags = SHGFI_ICON | SHGFI_SMALLICON;
                uint attrs = isDir ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;

                // Для сетевых путей или стандартных расширений (кроме локальных exe/ico)
                // всегда используем виртуальные системные атрибуты без обращения к диску или сети!
                bool useAttributesOnly = isUnc || isDir || (ext != ".exe" && ext != ".ico");

                if (useAttributesOnly)
                {
                    flags |= SHGFI_USEFILEATTRIBUTES;
                    string queryPath = isDir ? "dummy_folder" : (string.IsNullOrEmpty(ext) ? "dummy_file" : ext);
                    IntPtr res = SHGetFileInfo(queryPath, attrs, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
                    if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                    {
                        using (Icon ico = Icon.FromHandle(shinfo.hIcon))
                        {
                            Bitmap bmp = new Bitmap(ico.ToBitmap());
                            DestroyIcon(shinfo.hIcon);
                            return bmp;
                        }
                    }
                }
                else
                {
                    // Локальный exe/ico файл
                    try
                    {
                        if (File.Exists(path))
                        {
                            using (Icon ico = Icon.ExtractAssociatedIcon(path))
                            {
                                if (ico != null) return new Bitmap(ico.ToBitmap());
                            }
                        }
                    }
                    catch {}

                    flags |= SHGFI_USEFILEATTRIBUTES;
                    IntPtr res = SHGetFileInfo(ext, attrs, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
                    if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                    {
                        using (Icon ico = Icon.FromHandle(shinfo.hIcon))
                        {
                            Bitmap bmp = new Bitmap(ico.ToBitmap());
                            DestroyIcon(shinfo.hIcon);
                            return bmp;
                        }
                    }
                }
            }
            catch {}

            return SystemIcons.Application.ToBitmap();
        }
    }
}
