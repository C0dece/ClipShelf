using System;
using System.IO;
using System.Threading;

namespace PasteImageAsFile
{
    public static class Logger
    {
        private const long MaxLogSizeBytes = 2 * 1024 * 1024; // 2 МБ

        public static void Log(string msg)
        {
            try
            {
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string path = Path.Combine(localApp, "ClipShelf", "app.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string line = DateTime.Now.ToString("HH:mm:ss.fff") + " [" + Thread.CurrentThread.ManagedThreadId + "] " + msg;
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(line + Environment.NewLine);
                using (var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    if (fs.Length > MaxLogSizeBytes)
                    {
                        fs.SetLength(0);
                    }
                    fs.Seek(0, SeekOrigin.End);
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush();
                }
            }
            catch {}
        }
    }
}
