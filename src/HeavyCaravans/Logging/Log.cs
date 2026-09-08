using System;
using TaleWorlds.Library;

namespace HeavyCaravans.Logging
{
    /// <summary>
    /// Minimal logger: in-game message for warnings/errors (visible without opening a log file),
    /// plus a rolling text file under the game's Logs folder for post-mortem debugging.
    /// See phases/15-logging.md.
    /// </summary>
    public static class Log
    {
        private const string Prefix = "[HeavyCaravans] ";

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
            InformationManager.DisplayMessage(new InformationMessage(Prefix + message, new Color(1f, 0.85f, 0.1f)));
        }

        public static void Error(string message, Exception exception = null)
        {
            string full = exception == null ? message : message + " -- " + exception;
            Write("ERROR", full);
            InformationManager.DisplayMessage(new InformationMessage(Prefix + message, new Color(0.9f, 0.15f, 0.15f)));
        }

        private static void Write(string level, string message)
        {
            try
            {
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                System.IO.File.AppendAllText(LogFilePath.Value, line + Environment.NewLine);
            }
            catch
            {
                // Logging must never crash the game - swallow any IO failure.
            }
        }

        private static readonly Lazy<string> LogFilePath = new Lazy<string>(() =>
        {
            string dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Mount and Blade II Bannerlord", "Logs");
            System.IO.Directory.CreateDirectory(dir);
            return System.IO.Path.Combine(dir, "HeavyCaravans.log");
        });
    }
}
