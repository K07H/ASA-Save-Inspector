using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ASA_Save_Inspector
{
    internal sealed class Logger
    {
        public enum LogLevel
        {
            DEBUG = 0,
            INFO = 1,
            WARNING = 2,
            ERROR = 3
        }

        private static Logger? _instance = null;
        private static readonly object padlock = new object();

        private bool _outputToConsole = true;
        private string CurrentDateTime() => DateTime.Now.ToString("dd-MM-yyyy HH\\hmm\\mss.f\\s");

        private Logger()
        {
            if (!Directory.Exists(Utils.GetDataDir()))
            {
                try { Directory.CreateDirectory(Utils.GetDataDir()); }
                catch { }
            }
            string logsFilePath = Utils.LogsFilePath();
            if (File.Exists(logsFilePath))
            {
                try { File.Copy(logsFilePath, Utils.PreviousLogsFilePath(), true); }
                catch { }
            }
            try { File.WriteAllText(logsFilePath, $"{CurrentDateTime()}: INFO: ASA Save Inspector has started.{Environment.NewLine}", Encoding.UTF8); }
            catch (Exception ex) { Debug.WriteLine($"ERROR: Logger initialization failed. Exception=[{ex}]"); }
        }

        public static Logger Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (padlock)
                    {
                        if (_instance == null)
                            _instance = new Logger();
                    }
                }
                return _instance;
            }
        }

        public void Log(string message, LogLevel level = LogLevel.INFO)
        {
            string prefix = string.Empty;
            switch (level)
            {
                case LogLevel.DEBUG:
                    prefix = "DEBUG";
                    break;
                case LogLevel.WARNING:
                    prefix = "WARNING";
                    break;
                case LogLevel.ERROR:
                    prefix = "ERROR";
                    break;
                default:
                    prefix = "INFO";
                    break;
            }

            File.AppendAllTextAsync(Utils.LogsFilePath(), $"{CurrentDateTime()}: {prefix}: {message}{Environment.NewLine}", Encoding.UTF8);
            if (_outputToConsole)
                Debug.WriteLine($"{prefix}: {message}");
        }
    }
}
