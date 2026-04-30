using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace MVP_TextGame
{
    public static class GameLogger
    {
        private static readonly string _logFile = "server_log.txt";
        private static readonly object _lock = new object();

        public static void Log(string message)
        {
            string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

            Console.WriteLine(logEntry);

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_logFile, logEntry + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"!!! ERROR LOGGING TO FILE: {ex.Message}");
                }
            }
        }
    }
}
