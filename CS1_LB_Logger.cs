using System;
using System.IO;

namespace TaxiMod
{
    internal static class CS1_LB_Logger
    {
        private static string _logPath;
        private static bool _ready;

        public static void Init()
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                _logPath = Path.Combine(desktop, "CS1_LaneBalancer.LOG");

                // IMMER überschreiben
                File.WriteAllText(_logPath,
                    "=== CS1_LaneBalancer LOG ===\r\n" +
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n");

                _ready = true;
                Log("LogInit OK. Path=" + _logPath);
            }
            catch
            {
                _ready = false;
            }
        }

        public static void Log(string msg)
        {
            try
            {
                if (!_ready || string.IsNullOrEmpty(_logPath)) return;
                File.AppendAllText(_logPath,
                    DateTime.Now.ToString("HH:mm:ss") + " | " + msg + "\r\n");
            }
            catch { }
        }
    }
}
