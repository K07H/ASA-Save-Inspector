using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

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

        /// <summary>Messages below this level are ignored (DEBUG messages are only kept in Debug builds).</summary>
#if DEBUG
        public LogLevel MinimumLevel { get; set; } = LogLevel.DEBUG;
#else
        public LogLevel MinimumLevel { get; set; } = LogLevel.INFO;
#endif

        /// <summary>Longer messages are truncated, so that a single huge message cannot bloat the logs file.</summary>
        private const int MAX_MESSAGE_LENGTH = 10000;

        private StreamWriter? _writer = null;

        private readonly BlockingCollection<(string? Line, ManualResetEventSlim? FlushEvent)> _queue = new BlockingCollection<(string? Line, ManualResetEventSlim? FlushEvent)>();
        private readonly Thread _writerThread;
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
            try
            {
                _writer = new StreamWriter(new FileStream(logsFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete), Encoding.UTF8);
                _writer.Write($"{CurrentDateTime()}: INFO: ASA Save Inspector has started.{Environment.NewLine}");
                _writer.Flush();
            }
            catch (Exception ex)
            {
                _writer = null;
                Debug.WriteLine($"ERROR: Logger initialization failed. Exception=[{ex}]");
            }

            _writerThread = new Thread(WriterLoop) { IsBackground = true, Name = "ASI logger" };
            _writerThread.Start();
        }

        private void WriterLoop()
        {
            foreach ((string? line, ManualResetEventSlim? flushEvent) in _queue.GetConsumingEnumerable())
            {
                try
                {
                    if (line != null)
                    {
                        if (_writer != null)
                            _writer.Write(line);
                        else
                            File.AppendAllText(Utils.LogsFilePath(), line, Encoding.UTF8); // Fallback if the logs file could not be opened at startup.
                    }
                    // Lines are written in batches: the file is flushed once the queue is empty (or when Flush() is called).
                    if (_writer != null && (flushEvent != null || _queue.Count == 0))
                        _writer.Flush();
                }
                catch (Exception ex) { Debug.WriteLine($"ERROR: Failed to write log. Exception=[{ex}]"); }
                try { flushEvent?.Set(); }
                catch { }
            }
        }

        public void Flush(int timeoutMilliseconds = 3000)
        {
            try
            {
                ManualResetEventSlim done = new ManualResetEventSlim(false);
                _queue.Add((null, done));
                done.Wait(timeoutMilliseconds);
            }
            catch { }
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
            if (level < MinimumLevel)
                return;
            if (message != null && message.Length > MAX_MESSAGE_LENGTH)
                message = $"{message.Substring(0, MAX_MESSAGE_LENGTH)}... (message truncated, {message.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)} characters)";

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

            try { _queue.Add(($"{CurrentDateTime()}: {prefix}: {message}{Environment.NewLine}", null)); }
            catch { }
            if (_outputToConsole)
                Debug.WriteLine($"{prefix}: {message}");
        }
    }
}
