using System;
using System.IO;

namespace BotWhatsappCSharp.Services
{
    public static class Logger
    {
        private static readonly string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BotZapAI", "logs");
        private static readonly object _lock = new object();

        static Logger()
        {
            try
            {
                if (!Directory.Exists(LogDir))
                    Directory.CreateDirectory(LogDir);
            }
            catch { }
        }

        public static void Log(string message, string type = "INFO")
        {
            try
            {
                string filename = $"log_{DateTime.Now:yyyy-MM-dd}.txt";
                string path = Path.Combine(LogDir, filename);
                string logLine = $"[{DateTime.Now:HH:mm:ss}] [{type}] {message}{Environment.NewLine}";

                lock (_lock)
                {
                    File.AppendAllText(path, logLine);
                }
            }
            catch 
            {
                // Falha silenciosa no log para não crashar o app
            }
        }

        public static void Error(string message, Exception? ex = null)
        {
            string msg = message;
            if (ex != null)
            {
                msg += $" | EX: {ex.Message} | STACK: {ex.StackTrace}";
            }
            Log(msg, "ERROR");
        }
    }
}
