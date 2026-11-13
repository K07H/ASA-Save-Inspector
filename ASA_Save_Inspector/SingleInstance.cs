using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

namespace ASA_Save_Inspector
{
    /// <summary>Ensures that only one ASI instance runs at a time.</summary>
    internal static class SingleInstance
    {
        /// <summary>Exit code returned when another ASI instance is already running.</summary>
        public const int EXIT_ALREADY_RUNNING = 6;

        /// <summary>How long a CLI launch waits for a previous instance to exit (ex: "-CleanOldData" launched right after "-ExtractPreset").</summary>
        public const int CLI_WAIT_MILLISECONDS = 30000;

        /// <summary>How long a GUI launch waits (absorbs double-clicks and an instance that is closing).</summary>
        public const int GUI_WAIT_MILLISECONDS = 2000;

        /// <summary>One mutex per Windows session. Each Windows user has his own data folder (LocalAppData), so different users do not conflict.</summary>
        private const string MUTEX_NAME = @"Local\ASA_Save_Inspector_SingleInstance_ABCD4242";

        /// <summary>Kept for the whole process lifetime (the mutex is released by Windows when the process exits).</summary>
        private static Mutex? _mutex = null;

        private static readonly string[] _cliArguments = new string[] { "-ExtractPreset", "-CleanOldData" };

        /// <summary>True if ASI has been launched with a CLI argument (no user in front of the screen).</summary>
        public static bool IsCliLaunch()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                if (args == null || args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
                    return false;
                foreach (string cliArg in _cliArguments)
                    if (string.Equals(args[1], cliArg, StringComparison.OrdinalIgnoreCase))
                        return true;
            }
            catch { }
            return false;
        }

        /// <summary>Tries to become the single running instance. Returns false if another instance still runs after waitMilliseconds.</summary>
        public static bool TryAcquire(int waitMilliseconds)
        {
            try
            {
                _mutex = new Mutex(false, MUTEX_NAME);
                try
                {
                    return _mutex.WaitOne(waitMilliseconds);
                }
                catch (AbandonedMutexException)
                {
                    // The previous instance exited without releasing the mutex (crash, kill, or normal exit): we now own it.
                    return true;
                }
            }
            catch
            {
                // Should never happen. If the mutex cannot be used, do not prevent the user from using ASI.
                return true;
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

        private const uint MB_OK = 0x00000000;
        private const uint MB_ICONINFORMATION = 0x00000040;
        private const uint MB_SETFOREGROUND = 0x00010000;
        private const uint MB_TOPMOST = 0x00040000;

        /// <summary>Shows a native message box (WinUI and translations are not initialized yet at this point).</summary>
        public static void ShowAlreadyRunningMessage()
        {
            bool isFrench = string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "fr", StringComparison.OrdinalIgnoreCase);
            string text = isFrench
                ? "ASA Save Inspector est déjà en cours d'exécution (fenêtre ouverte ou extraction automatique en cours).\n\nFermez l'autre instance puis réessayez."
                : "ASA Save Inspector is already running (window already open or automatic extraction in progress).\n\nPlease close the other instance and try again.";
            try { MessageBoxW(IntPtr.Zero, text, "ASA Save Inspector", MB_OK | MB_ICONINFORMATION | MB_SETFOREGROUND | MB_TOPMOST); }
            catch { }
        }
    }
}
