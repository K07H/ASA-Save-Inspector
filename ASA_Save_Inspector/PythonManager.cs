using ASA_Save_Inspector.Pages;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASA_Save_Inspector
{
    class PythonManager
    {
        public static readonly HttpClient _client = new HttpClient();

        public static void InitHttpClient()
        {
            if (_client != null)
                _client.DefaultRequestHeaders.UserAgent.ParseAdd($"ASASaveInspector/{Utils.GetVersionStr()}");
        }

        public static List<string>? GetPythonExePaths()
        {
            List<string> result = new List<string>();
            try
            {
                List<string>? paths = Utils.GetPathsFromEnvironmentVariables();
                if (paths == null || paths.Count <= 0)
                    return null;
                foreach (string path in paths)
                    if (Directory.Exists(path) && path.Contains("Python", StringComparison.InvariantCultureIgnoreCase))
                    {
                        string exePath = Path.Combine(path, "python.exe");
                        if (File.Exists(exePath) && !result.Contains(exePath))
                            result.Add(exePath);
                    }
                string pythonFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python");
                if (Directory.Exists(pythonFolder))
                {
                    string[] dirs = Directory.GetDirectories(pythonFolder, "Python3*", SearchOption.TopDirectoryOnly);
                    if (dirs != null && dirs.Length > 0)
                        foreach (string dir in dirs)
                            if (!string.IsNullOrWhiteSpace(dir))
                            {
                                string exePath = Path.Combine(dir, "python.exe");
                                if (File.Exists(exePath) && !result.Contains(exePath))
                                    result.Add(exePath);
                            }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ASILang.Get("ArkParseSearchPythonError")} Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            return result;
        }

        internal static string BatchEscape(string? value) => (value ?? string.Empty).Replace("%", "%%", StringComparison.Ordinal);

        internal static string BatchPythonExe() => BatchEscape(SettingsPage.UsePythonVenv() ? Utils.PythonFilePathFromVenv() : SettingsPage._pythonExePath);

        internal static string BatchPythonPackagesPath() => (SettingsPage.UsePythonVenv() ? $"{BatchEscape(Path.Combine(Utils.PythonVenvFolder(), "Lib", "site-packages"))}" : string.Empty);

        internal static string BatchPythonPathLine() => (SettingsPage.UsePythonVenv() ? "set \"PYTHONPATH=\"" : string.Empty);

        /// <summary>Writes a .bat file in UTF-8 (without BOM) and switches cmd.exe to code page 65001 first, so that paths containing non-ASCII characters (ex: "C:\Users\Testé\...") are not converted to "?".</summary>
        internal static void WriteBatchFile(string path, string content) => File.WriteAllText(path, "@chcp 65001 >nul" + Environment.NewLine + content, new UTF8Encoding(false));

        public static bool IsArkParsePresent() => File.Exists(Utils.ArkParseJsonApiFilePath());

        #region Busy state (only one extraction at a time, never during an ArkParse install/update)

        // ID of the running extraction (0 = none). An ID (instead of a simple flag) ensures that an extraction can only release its own lock, never the lock of the next extraction of a preset.
        private static long _extractionCounter = 0;
        private static long _runningExtractionId = 0;

        // 1 while ArkParse (or Python's venv) is being installed/updated. Released by HideInstallingPopup, which ends every install path.
        private static int _arkParseInstallRunning = 0;

        public static bool IsExtractionRunning => Interlocked.Read(ref _runningExtractionId) != 0;

        public static bool IsArkParseInstallRunning => Volatile.Read(ref _arkParseInstallRunning) != 0;

        /// <summary>True while an extraction or an ArkParse install/update is running.</summary>
        public static bool IsBusy => IsExtractionRunning || IsArkParseInstallRunning;

        private static readonly object _busyLock = new object();

        /// <summary>Returns the ID of the new extraction, or 0 if another extraction (or an ArkParse install) is running.</summary>
        private static long TryBeginExtraction()
        {
            lock (_busyLock)
            {
                if (IsArkParseInstallRunning || IsExtractionRunning)
                    return 0;
                long id = Interlocked.Increment(ref _extractionCounter);
                Interlocked.Exchange(ref _runningExtractionId, id);
                return id;
            }
        }

        private static void EndExtraction(long extractionId)
        {
            if (extractionId == 0)
                return;
            lock (_busyLock)
                Interlocked.CompareExchange(ref _runningExtractionId, 0, extractionId);
        }

        private static bool TryBeginArkParseInstall()
        {
            lock (_busyLock)
            {
                if (IsExtractionRunning)
                    return false;
                Interlocked.Exchange(ref _arkParseInstallRunning, 1);
                return true;
            }
        }

        private static void EndArkParseInstall()
        {
            lock (_busyLock)
                Interlocked.Exchange(ref _arkParseInstallRunning, 0);
        }

        /// <summary>Logs and shows (toast) why an extraction or an install cannot be started now. Returns false (for convenience).</summary>
        public static bool NotifyBusy()
        {
            string msg = (IsExtractionRunning ? ASILang.Get("ExtractionAlreadyRunning") : ASILang.Get("ArkParseInstallAlreadyRunning"));
            Logger.Instance.Log(msg, Logger.LogLevel.WARNING);
            MainWindow.ShowToast(msg, BackgroundColor.WARNING);
            return false;
        }

        #endregion

        private static bool DeletePreviousBatchFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Unable to delete the previous script at '{path}'. Exception=[{ex}]", Logger.LogLevel.ERROR);
                return false;
            }
        }

        private static void RefreshJsonExportProfilesDatagrid()
        {
            if (SettingsPage._page != null)
                SettingsPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    SettingsPage._page.InitDatagrid(false, false);
                });
        }

        private static void DiscardFailedExtraction(JsonExportProfile? jep, string reason)
        {
            if (jep == null)
                return;
            try
            {
                Logger.Instance.Log($"Extraction '{jep.GetLabel()}' failed ({reason}): its export profile and its export folder are removed.", Logger.LogLevel.WARNING);
                if (SettingsPage.DeleteJsonExportProfile(jep) < 0)
                    Logger.Instance.Log($"Failed to remove the export profile of failed extraction '{jep.GetLabel()}'.", Logger.LogLevel.WARNING);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught while removing the export profile of failed extraction '{jep.GetLabel()}'. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            RefreshJsonExportProfilesDatagrid();
        }

        public static async Task<bool> DownloadArkParse()
        {
            string filePath = Utils.ArkParseArchiveFilePath();
            string partFilePath = filePath + ".part";
            try
            {
                Utils.EnsureDataFolderExist();
                using (var downloadStream = await _client.GetStreamAsync(Utils.ArkParseArchiveUrl))
                using (var fileStream = new FileStream(partFilePath, FileMode.Create, FileAccess.Write))
                {
                    await downloadStream.CopyToAsync(fileStream);
                    await fileStream.FlushAsync();
                }

                File.Move(partFilePath, filePath, true);
                if (File.Exists(filePath))
                    return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ASILang.Get("ArkParseDownloadingError")} Exception=[{ex}]", Logger.LogLevel.ERROR);
                try { if (File.Exists(partFilePath)) File.Delete(partFilePath); }
                catch (Exception ignoredEx) { Utils.LogIgnoredException("PythonManager.DownloadArkParse", ignoredEx); }
            }
            return false;
        }

        private static string? GetLocalArkParseVersion()
        {
            string arkParseVersionFilePath = Utils.ArkParseVersionFilePath();
            if (!File.Exists(arkParseVersionFilePath))
            {
                Logger.Instance.Log($"{ASILang.Get("GetArkParseVersionFailed")} {ASILang.Get("ArkParseVersionFileNotFound")}", Logger.LogLevel.WARNING);
                return null;
            }
            try
            {
                string[] lines = File.ReadAllLines(arkParseVersionFilePath);
                if (lines == null || lines.Length <= 0)
                {
                    Logger.Instance.Log($"{ASILang.Get("GetArkParseVersionFailed")} {ASILang.Get("ArkParseVersionFileIsEmpty")}", Logger.LogLevel.WARNING);
                    return null;
                }
                return lines[0];
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ASILang.Get("ArkParseVersionFileFailedReading")} Exception=[{ex}]", Logger.LogLevel.ERROR);
                return null;
            }
        }

        private static async Task<string?> GetRepoArkParseVersion()
        {
            try
            {
                return await GetRepoArkParseVersionCore();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ASILang.Get("GetArkParseVersionFailed")} Exception=[{ex}]", Logger.LogLevel.WARNING);
                return null;
            }
        }

        private static async Task<string?> GetRepoArkParseVersionCore()
        {
            string projectFileContent = await _client.GetStringAsync(Utils.ArkParseVersionFileUrl);
            if (string.IsNullOrWhiteSpace(projectFileContent))
            {
                Logger.Instance.Log($"{ASILang.Get("GetArkParseVersionFailed")} {ASILang.Get("RepoArkParseVersionFileNotFound")}", Logger.LogLevel.WARNING);
                return null;
            }
            string[] lines = projectFileContent.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines == null || lines.Length <= 0)
            {
                Logger.Instance.Log($"{ASILang.Get("GetArkParseVersionFailed")} {ASILang.Get("RepoArkParseVersionFileIsEmpty")}", Logger.LogLevel.WARNING);
                return null;
            }
            return lines[0];
        }

        private static void ExtractArkParseArchive(string archiveFilePath)
        {
            string dataDir = Utils.GetDataDir();
            string expectedFolder = Utils.ArkParseFolder();
            string tempFolder = Path.Combine(dataDir, "ark-save-parser-new");
            string backupFolder = Path.Combine(dataDir, "ark-save-parser-old");

            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);
            if (Directory.Exists(backupFolder))
                Directory.Delete(backupFolder, true);

            ZipFile.ExtractToDirectory(archiveFilePath, tempFolder);

            string newRoot = tempFolder;
            string[] topDirs = Directory.GetDirectories(tempFolder);
            if (topDirs.Length == 1 && Directory.GetFiles(tempFolder).Length == 0)
                newRoot = topDirs[0];

            string jsonApiRelativePath = Path.GetRelativePath(expectedFolder, Utils.ArkParseJsonApiFilePath());
            if (!File.Exists(Path.Combine(newRoot, jsonApiRelativePath)))
            {
                try { Directory.Delete(tempFolder, true); }
                catch (Exception ignoredEx) { Utils.LogIgnoredException("PythonManager.ExtractArkParseArchive", ignoredEx); }
                throw new InvalidDataException($"The ArkParse archive does not contain '{jsonApiRelativePath}'.");
            }

            if (Directory.Exists(expectedFolder))
                Directory.Move(expectedFolder, backupFolder);
            try
            {
                Directory.Move(newRoot, expectedFolder);
            }
            catch
            {
                if (Directory.Exists(backupFolder) && !Directory.Exists(expectedFolder))
                    Directory.Move(backupFolder, expectedFolder);
                throw;
            }

            try { if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true); }
            catch (Exception ignoredEx) { Utils.LogIgnoredException("PythonManager.ExtractArkParseArchive", ignoredEx); }
            try { if (Directory.Exists(backupFolder)) Directory.Delete(backupFolder, true); }
            catch (Exception ignoredEx) { Utils.LogIgnoredException("PythonManager.ExtractArkParseArchive", ignoredEx); }
        }

        public static async Task<bool> DownloadAndExtractArkParse()
        {
            if (IsArkParsePresent())
            {
                string? localVersion = GetLocalArkParseVersion();
                string? repoVersion = await GetRepoArkParseVersion();
                if (string.IsNullOrWhiteSpace(repoVersion))
                {
                    // Unable to get the latest version, keep the currently installed version.
                    AddDetailsToInstallingPopup(ASILang.Get("GetArkParseVersionFailed"), true);
                    return false;
                }
                if (string.Compare(localVersion, repoVersion, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    AddDetailsToInstallingPopup(ASILang.Get("ArkParseUpToDate"), false, false, true);
                    return false;
                }
                AddDetailsToInstallingPopup(ASILang.Get("ArkParseUpdating"));
            }
            else
                AddDetailsToInstallingPopup(ASILang.Get("ArkParseDownloading"));

            bool downloaded = await DownloadArkParse();
            if (!downloaded)
            {
                AddDetailsToInstallingPopup(ASILang.Get("ArkParseDownloadingFail"), false, true);
                return false;
            }
            AddDetailsToInstallingPopup(ASILang.Get("ArkParseDownloadingSuccess"), false, false, true);

            string filePath = Utils.ArkParseArchiveFilePath();
            try
            {
                AddDetailsToInstallingPopup(ASILang.Get("ArkParseArchiveExtracting"));
                ExtractArkParseArchive(filePath);
                if (File.Exists(filePath))
                    try { File.Delete(filePath); }
                    catch (Exception ignoredEx) { Utils.LogIgnoredException("PythonManager.DownloadAndExtractArkParse", ignoredEx); }
                if (IsArkParsePresent())
                {
                    AddDetailsToInstallingPopup(ASILang.Get("ArkParseArchiveExtractingSuccess"), false, false, true);
                    return true;
                }
                else
                {
                    AddDetailsToInstallingPopup(ASILang.Get("ArkParseArchiveExtractingFail"), false, true);
                    return false;
                }
            }
            catch (Exception ex)
            {
                AddDetailsToInstallingPopup($"{ASILang.Get("ArkParseDownloadAndExtractError")} Exception=[{ex}]", false, true);
            }

            if (File.Exists(filePath))
                try { File.Delete(filePath); }
                catch (Exception ignoredEx) { Utils.LogIgnoredException("PythonManager.DownloadAndExtractArkParse", ignoredEx); }
            return false;
        }

#pragma warning disable CS1998
        public static async Task<bool> SetupPythonVenv()
        {
            // Never install/update ArkParse during an extraction (pip would modify ArkParse while it is being used).
            // (Callers check IsBusy BEFORE showing the popup: the popup must not be hidden here, it may be the popup of the running extraction.)
            if (!TryBeginArkParseInstall())
                return NotifyBusy();
            try
            {
                return await SetupPythonVenvCore();
            }
            catch
            {
                EndArkParseInstall(); // Normally released by HideInstallingPopup, at the end of the install.
                throw;
            }
        }

        private static async Task<bool> SetupPythonVenvCore()
        {
            if (!SettingsPage.UsePythonVenv())
            {
                await UpdateOrInstallArkParse();
                return true;
            }

            if (Directory.Exists(Utils.PythonVenvFolder()))
            {
                Logger.Instance.Log(ASILang.Get("PythonVenvAlreadySetup"));
                await UpdateOrInstallArkParse();
                return true;
            }

            AddDetailsToInstallingPopup(ASILang.Get("PythonVenvSetup"));
            Task.Run(async () =>
            {
                Process? process = null;
                int exitCode = -1;
                try
                {
                    process = new Process();
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.FileName = Utils.PythonVenvSetupFilePath();
                    process.StartInfo.WorkingDirectory = Utils.GetDataDir();
                    process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                    process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                    process.OutputDataReceived += ShowProcessOutput;
                    process.ErrorDataReceived += ShowProcessError;
                    ResetProcessLogs();
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    AddDetailsToInstallingPopup($"{ASILang.Get("PythonVenvSetupError")} Exception=[{ex}]", false, true);
                }
                finally
                {
                    try
                    {
                        if (process != null)
                            exitCode = process.ExitCode;
                    }
                    catch { exitCode = -1; }
                    if (process != null)
                    {
                        process.Dispose();
                        process = null;
                    }
                    if (exitCode == 0)
                        AddDetailsToInstallingPopup(ASILang.Get("PythonVenvSetupSuccess"), false, false, true);
                    else
                        AddDetailsToInstallingPopup(ASILang.Get("PythonVenvSetupFail"), false, true);
                    Logger.Instance.Log($"{ASILang.Get("PythonExitCode")}: {exitCode.ToString(CultureInfo.InvariantCulture)}", Logger.LogLevel.INFO);

                    await UpdateOrInstallArkParse();
                }
            }).FireAndForget("SetupPythonVenv (venv creation)");

            return true;
        }

        private static void HandleDistUtilsPrecedenceError()
        {
            string distUtilsPrecedenceFilePath = Path.Combine(Utils.PythonVenvFolder(), "Lib", "site-packages", "distutils-precedence.pth");
            Logger.Instance.Log($"{ASILang.Get("PythonDistUtilsPrecedenceError").Replace("#FILEPATH#", distUtilsPrecedenceFilePath ?? string.Empty, StringComparison.InvariantCulture)}", Logger.LogLevel.INFO);
            try
            {
                if (File.Exists(distUtilsPrecedenceFilePath))
                    File.Delete(distUtilsPrecedenceFilePath);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ASILang.Get("DeleteFileAtFailed").Replace("#FILEPATH#", distUtilsPrecedenceFilePath ?? string.Empty, StringComparison.InvariantCulture)} Exception=[{ex}]", Logger.LogLevel.WARNING);
            }
        }

        private static void DoUpdateOrInstallArkParse(string arkParseSetupPath, bool checkForDistUtilsPrecedenceError)
        {
            AddDetailsToInstallingPopup(ASILang.Get("ArkParseInstalling"));
            Process? process = null;
            int exitCode = -1;
            try
            {
                process = new Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = arkParseSetupPath;
                process.StartInfo.WorkingDirectory = Utils.ArkParseFolder();
                process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                process.OutputDataReceived += ShowProcessOutput;
                process.ErrorDataReceived += ShowProcessError;
                ResetProcessLogs();
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                AddDetailsToInstallingPopup($"{ASILang.Get("ArkParseInstallError")} Exception=[{ex}]", false, true);
            }
            finally
            {
                try
                {
                    if (process != null)
                        exitCode = process.ExitCode;
                }
                catch { exitCode = -1; }
                if (process != null)
                {
                    try { process.Dispose(); }
                    catch (Exception ignoredEx) { Utils.LogIgnoredException("PythonManager.DoUpdateOrInstallArkParse", ignoredEx); }
                    process = null;
                }
                if (exitCode == 0)
                {
                    AddDetailsToInstallingPopup(ASILang.Get("ArkParseInstallSuccess"), false, false, true);
                    if (RequiresArkParseReinstall())
                        try { File.Delete(Utils.ArkParseRequiresReinstallFilePath()); }
                        catch (Exception exb) { Logger.Instance.Log($"Exception caught while deleting file at '{Utils.ArkParseRequiresReinstallFilePath()}'. Exception=[{exb}]", Logger.LogLevel.ERROR); }
                }
                else
                    AddDetailsToInstallingPopup(ASILang.Get("ArkParseInstallFail"), false, true);
                Logger.Instance.Log($"{ASILang.Get("PythonExitCode")}: {exitCode.ToString(CultureInfo.InvariantCulture)}", Logger.LogLevel.INFO);
                if (checkForDistUtilsPrecedenceError && exitCode != 0 && HasDistUtilsPrecedenceError())
                {
                    // Handle distutils-precedence.pth error.
                    HandleDistUtilsPrecedenceError();
                    // Now re-run ArkParse update/install.
                    DoUpdateOrInstallArkParse(arkParseSetupPath, false);
                }
                else
                    HideInstallingPopup().FireAndForget(nameof(HideInstallingPopup));
            }
        }

        public static bool RequiresArkParseReinstall() => File.Exists(Utils.ArkParseRequiresReinstallFilePath());

        public static async Task<bool> UpdateOrInstallArkParse()
        {
            try
            {
                return await UpdateOrInstallArkParseCore();
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ASILang.Get("ArkParseInstallError")} Exception=[{ex}]", Logger.LogLevel.ERROR);
                await HideInstallingPopup();
                return false;
            }
        }

        private static async Task<bool> UpdateOrInstallArkParseCore()
        {
            if (SettingsPage._debugLogging != null && SettingsPage._debugLogging.HasValue && SettingsPage._debugLogging.Value)
                Utils.LogASIDataFolder();

            if (SettingsPage.UsePythonVenv())
            {
                if (!File.Exists(Utils.PythonFilePathFromVenv()))
                {
                    HideInstallingPopup().FireAndForget(nameof(HideInstallingPopup));
                    return false;
                }
            }
            else
            {
                if (!File.Exists(SettingsPage._pythonExePath))
                {
                    HideInstallingPopup().FireAndForget(nameof(HideInstallingPopup));
                    return false;
                }
            }

            if (SettingsPage._debugLogging != null && SettingsPage._debugLogging.HasValue && SettingsPage._debugLogging.Value)
                AddPythonTestFile();

            if (!AddArkParseSetup())
            {
                Logger.Instance.Log($"{ASILang.Get("ArkParseAddSetupError")} (C)", Logger.LogLevel.ERROR);
                HideInstallingPopup().FireAndForget(nameof(HideInstallingPopup));
                return false;
            }
            if (!(await DownloadAndExtractArkParse()) && !RequiresArkParseReinstall())
            {
                HideInstallingPopup().FireAndForget(nameof(HideInstallingPopup));
                return false;
            }
            if (!Directory.Exists(Utils.ArkParseFolder()))
            {
                HideInstallingPopup().FireAndForget(nameof(HideInstallingPopup));
                return false;
            }
            string arkParseSetupPath = Utils.ArkParseSetupFilePath();
            if (string.IsNullOrWhiteSpace(arkParseSetupPath) || !File.Exists(arkParseSetupPath))
            {
                HideInstallingPopup().FireAndForget(nameof(HideInstallingPopup));
                return false;
            }

            Task.Run(() => DoUpdateOrInstallArkParse(arkParseSetupPath, true)).FireAndForget(nameof(DoUpdateOrInstallArkParse));
            return true;
        }

#pragma warning restore CS1998

        public static bool CreateAsiExportScriptFile(string? asiExportFilePathSource, string? asiExportFilePathDest)
        {
            if (string.IsNullOrEmpty(asiExportFilePathSource))
            {
                Logger.Instance.Log(ASILang.Get("ASIExportScript_BadSourceFilePath"), Logger.LogLevel.ERROR);
                return false;
            }
            if (!File.Exists(asiExportFilePathSource))
            {
                Logger.Instance.Log($"{ASILang.Get("ASIExportScript_NotFound").Replace("#FILEPATH#", Path.GetFileName(asiExportFilePathSource) ?? string.Empty, StringComparison.InvariantCulture)}", Logger.LogLevel.ERROR);
                return false;
            }
            if (string.IsNullOrEmpty(asiExportFilePathDest))
            {
                Logger.Instance.Log(ASILang.Get("ASIExportScript_BadDestFilePath"), Logger.LogLevel.ERROR);
                return false;
            }
            if (Directory.Exists(asiExportFilePathDest) || (File.Exists(asiExportFilePathDest) && (File.GetAttributes(asiExportFilePathDest) & FileAttributes.Directory) == FileAttributes.Directory))
            {
                Logger.Instance.Log(ASILang.Get("ASIExportScript_DirectoryNotAllowed"), Logger.LogLevel.ERROR);
                return false;
            }

            try
            {
                Utils.EnsureDataFolderExist();
                string asiExportScriptContent = File.ReadAllText(asiExportFilePathSource, Encoding.UTF8);
                File.WriteAllText(asiExportFilePathDest, asiExportScriptContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ASILang.Get("ASIExportScript_Error")} Exception=[{ex}]", Logger.LogLevel.ERROR);
            }

            return File.Exists(asiExportFilePathDest);
        }

        public static bool AddPythonVenvSetup()
        {
            // Don't proceed if python venv is ALREADY present.
            if (File.Exists(Utils.PythonFilePathFromVenv()))
                return false;

            Utils.EnsureDataFolderExist();
            string pythonVenvSetupPath = Utils.PythonVenvSetupFilePath();
            string pythonVenvSetupContent = string.Format(
@"
""{0}"" -m venv python_venv
", BatchEscape(SettingsPage._pythonExePath));

            try
            {
                WriteBatchFile(pythonVenvSetupPath, pythonVenvSetupContent);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ASILang.Get("PythonVenvAddSetupError")} Exception=[{ex}]", Logger.LogLevel.ERROR);
            }

            return File.Exists(pythonVenvSetupPath);
        }

        public static bool AddArkParseSetup()
        {
            if (SettingsPage.UsePythonVenv())
            {
                // Don't proceed if python venv is NOT present.
                if (!File.Exists(Utils.PythonFilePathFromVenv()))
                    return false;
            }
            else
            {
                // Don't proceed if python global exe is NOT present.
                if (!File.Exists(SettingsPage._pythonExePath))
                    return false;
            }

            Utils.EnsureDataFolderExist();
            string arkParseSetupPath = Utils.ArkParseSetupFilePath();
            if (!DeletePreviousBatchFile(arkParseSetupPath))
                return false;
            string? arkParseSetupContent = null;
            if (SettingsPage._debugLogging != null && SettingsPage._debugLogging.HasValue && SettingsPage._debugLogging.Value)
                arkParseSetupContent = string.Format(
@"
{0}
echo PATH environment variable
echo %PATH%
""{1}"" ""{2}""
""{1}"" -m ensurepip --upgrade
""{1}"" -m pip install --upgrade pip
""{1}"" -m pip --version
""{1}"" -m pip show pip
""{1}"" -m pip list -v
""{1}"" -m pip freeze --path ""{3}""
",
BatchPythonPathLine(),
BatchPythonExe(),
BatchEscape(Utils.PythonTestScriptPath()),
BatchPythonPackagesPath());
            else
                arkParseSetupContent = string.Format(
@"
{0}
""{1}"" -m ensurepip --upgrade
""{1}"" -m pip install --upgrade pip
",
BatchPythonPathLine(),
BatchPythonExe());

            if (string.IsNullOrWhiteSpace(arkParseSetupContent))
            {
                Logger.Instance.Log($"{ASILang.Get("ArkParseAddSetupError")} (A)", Logger.LogLevel.ERROR);
                return false;
            }

            /*  Successfully tested with the following packages versions:
                contourpy==1.4.0
                cycler==0.12.1
                fonttools==4.68.0
                kiwisolver==1.5.1
                matplotlib==3.11.2
                numpy==2.5.4
                packaging==26.3
                pandas==3.1.1
                pillow==13.0.0
                pyparsing==3.3.3
                python-dateutil==4.0.0
                rcon==2.4.9
                setuptools==84.2.1
                tzdata==2026.6
                wheel==0.48.0
            */
            // Versions can be pinned in the optional "asi_constraints.txt" file shipped in the ArkParse archive (it applies to every pip call below).
            arkParseSetupContent += string.Format(
@"
if exist ""asi_constraints.txt"" (
    set ""PIP_CONSTRAINT=asi_constraints.txt""
    echo File asi_constraints.txt will be used ^(found^).
) else (
    echo File asi_constraints.txt will not be used ^(not found^).
)
""{0}"" -m pip install --upgrade --prefer-binary setuptools wheel rcon numpy tzdata matplotlib pandas
if errorlevel 1 exit /b 1
""{0}"" -m pip install -e .
",
BatchPythonExe());

            bool written = false;
            try { WriteBatchFile(arkParseSetupPath, arkParseSetupContent); written = true; }
            catch (Exception ex) { Logger.Instance.Log($"{ASILang.Get("ArkParseAddSetupError")} (B) Exception=[{ex}]", Logger.LogLevel.ERROR); }

            return written && File.Exists(arkParseSetupPath);
        }

        public static bool AddPythonTestFile()
        {
            Utils.EnsureDataFolderExist();

            string pythonTestScriptPath = Utils.PythonTestScriptPath();
            string pythonTestScriptContent =
@"
import sys
import os

print(""sys.path: "" + str(sys.path))
print(""PYTHONPATH: "" + str(os.getenv(""PYTHONPATH"")))
";

            try { File.WriteAllText(pythonTestScriptPath, pythonTestScriptContent, Encoding.ASCII); }
            catch (Exception ex) { Logger.Instance.Log($"{ASILang.Get("PythonAddTestError")} (A) Exception=[{ex}]", Logger.LogLevel.ERROR); }

            return File.Exists(pythonTestScriptPath);
        }

        public static bool AddArkParseRunner(JsonExportProfile? jep)
        {
            // Delete the runner of the previous extraction first: if anything below fails, it must not be executed again.
            if (!DeletePreviousBatchFile(Utils.ArkParseRunnerFilePath()))
                return false;

            if (SettingsPage.UsePythonVenv())
            {
                // Don't proceed if python venv is NOT present.
                if (!File.Exists(Utils.PythonFilePathFromVenv()))
                    return false;
            }
            else
            {
                // Don't proceed if python global exe is NOT present.
                if (!File.Exists(SettingsPage._pythonExePath))
                    return false;
            }

            if (jep == null)
                return false;
            if (string.IsNullOrWhiteSpace(jep.SaveFilePath) || !File.Exists(jep.SaveFilePath))
                return false;

            string asiExportAllPath = Utils.AsiExportFastFilePath();
            if (!File.Exists(asiExportAllPath))
                return false;

            Utils.EnsureDataFolderExist();

            string exportFolderPath = Utils.JsonExportsFolder();
            if (!Directory.Exists(exportFolderPath))
            {
                try
                {
                    Directory.CreateDirectory(exportFolderPath);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"{ASILang.Get("ArkParseAddRunnerError")} (A) Exception=[{ex}]", Logger.LogLevel.ERROR);
                    return false;
                }
            }

            // (The deprecated format check used to test the relative folder name, so its result depended on the current directory.)
            if (jep.IsDeprecatedFormat())
            {
                Logger.Instance.Log($"{ASILang.Get("DeprecatedExportProfileFormat").Replace("#JSON_EXPORT_PROFILE_NAME#", jep.GetLabel() ?? string.Empty, StringComparison.InvariantCulture)}", Logger.LogLevel.ERROR);
                return false;
            }
            string finalExportFolderPath = Path.Combine(exportFolderPath, jep.GetExportFolderName());
            if (!Directory.Exists(finalExportFolderPath))
            {
                try
                {
                    Directory.CreateDirectory(finalExportFolderPath);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"{ASILang.Get("ArkParseAddRunnerError")} (B) Exception=[{ex}]", Logger.LogLevel.ERROR);
                    return false;
                }
            }

            string customBlueprintsFilePath = SettingsPage.WriteCustomBlueprintsForExport();
            string arkParseRunnerPath = Utils.ArkParseRunnerFilePath();
            string? arkParseRunnerContent = null;
            if (SettingsPage._debugLogging != null && SettingsPage._debugLogging.HasValue && SettingsPage._debugLogging.Value && File.Exists(Utils.PythonTestScriptPath()))
                arkParseRunnerContent = string.Format(
@"
{0}
echo PATH environment variable
echo %PATH%
""{1}"" ""{2}""
",
BatchPythonPathLine(),
BatchPythonExe(),
BatchEscape(Utils.PythonTestScriptPath()));
            else
                arkParseRunnerContent = string.Format(
@"
{0}
",
BatchPythonPathLine());

            if (string.IsNullOrWhiteSpace(arkParseRunnerContent) && SettingsPage.UsePythonVenv())
            {
                Logger.Instance.Log($"{ASILang.Get("ArkParseAddRunnerError")} (C)", Logger.LogLevel.ERROR);
                return false;
            }

            arkParseRunnerContent += string.Format(
@"
""{0}"" ""{1}"" ""{2}"" ""{3}"" {4} {5} {6} {7} {8} {9} {10}{11}
",
BatchPythonExe(),
BatchEscape(asiExportAllPath),
BatchEscape(jep.SaveFilePath),
BatchEscape(finalExportFolderPath),
(jep.ExtractedDinos ? "1" : "0"),
(jep.ExtractedPlayerPawns ? "1" : "0"),
(jep.ExtractedItems ? "1" : "0"),
(jep.ExtractedStructures ? "1" : "0"),
(jep.ExtractedPlayers ? "1" : "0"),
(jep.ExtractedTribes ? "1" : "0"),
(SettingsPage._debugLogging != null && SettingsPage._debugLogging.HasValue && SettingsPage._debugLogging.Value ? "1" : "0"),
(!string.IsNullOrEmpty(customBlueprintsFilePath) ? $" \"{BatchEscape(customBlueprintsFilePath)}\"" : string.Empty));

            bool written = false;
            try { WriteBatchFile(arkParseRunnerPath, arkParseRunnerContent); written = true; }
            catch (Exception ex) { Logger.Instance.Log($"{ASILang.Get("ArkParseAddRunnerError")} (D) Exception=[{ex}]", Logger.LogLevel.ERROR); }

            return written && File.Exists(arkParseRunnerPath);
        }

        private static readonly string[] _pythonColorCodes = new string[7]
        {
            "\e[0m",
            "\e[91m",
            "\e[92m",
            "\e[93m",
            "\e[94m",
            "\e[95m",
            "\e[96m",
        };

        public static string StripPythonColors(string str)
        {
            for (int i = 0; i < 7; i++)
                str = str.Replace(_pythonColorCodes[i], "", StringComparison.InvariantCulture);
            return str.Replace("\e", "");
        }

        private static volatile Process? _runningArkParseProcess = null;

        /// <summary>Kills the running ArkParse extraction process and its children, if any.</summary>
        public static void KillRunningProcess()
        {
            Process? process = _runningArkParseProcess;
            if (process == null)
                return;
            try
            {
                if (!process.HasExited)
                {
                    Logger.Instance.Log("Killing running ArkParse process.", Logger.LogLevel.WARNING);
                    process.Kill(true);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Failed to kill ArkParse process. Exception=[{ex}]", Logger.LogLevel.WARNING);
            }
        }

        private static readonly object _processLock = new object();

        private static void ResetProcessLogs()
        {
            lock (_processLock)
            {
                _processOutput.Clear();
                _processErrors.Clear();
            }
        }

        private static bool HasDistUtilsPrecedenceError()
        {
            lock (_processLock)
                return _processErrors.Any(s => s.Contains("Error processing line 1 of ", StringComparison.InvariantCultureIgnoreCase) && s.Contains("distutils-precedence.pth", StringComparison.InvariantCultureIgnoreCase));
        }

        private static List<string> _processErrors = new List<string>();
        private static void ShowProcessError(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                string str = StripPythonColors(e.Data);
                //Logger.Instance.Log(str, Logger.LogLevel.ERROR);
                lock (_processLock) { _processErrors.Add(str); }
                AddDetailsToInstallingPopup(str, false, true);
            }
        }

        private static List<string> _processOutput = new List<string>();
        private static void ShowProcessOutput(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                string str = StripPythonColors(e.Data);
                //Logger.Instance.Log(str, Logger.LogLevel.INFO);
                lock (_processLock) { _processOutput.Add(str); }
                AddDetailsToInstallingPopup(str);
            }
        }

#pragma warning disable CS1998
        private static Brush _errorColor = new SolidColorBrush(Colors.Red);
        private static Brush _warningColor = new SolidColorBrush(Colors.Orange);
        private static Brush _successColor = new SolidColorBrush(Colors.Green);
        private static void AddDetailsToInstallingPopup(string txt, bool isWarning = false, bool isError = false, bool isSuccess = false)
        {
            if (MainWindow._mainWindow != null)
                MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () =>
                {
                    Logger.Instance.Log(txt, (isError ? Logger.LogLevel.ERROR : (isWarning ? Logger.LogLevel.WARNING : Logger.LogLevel.INFO)));
                    MainWindow._mainWindow.AddTextToPopupDetails(txt, (isError ? _errorColor : (isWarning ? _warningColor : (isSuccess ? _successColor : null))));
                });
        }

        public static void ShowInstallingPopup(string title)
        {
            if (MainWindow._mainWindow != null)
                MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () =>
                {
                    MainWindow._mainWindow.ShowPopup(title);
                });
        }

        public static async Task<bool> HideInstallingPopup(int delay = 1000)
        {
            bool ret = false;
            if (delay > 0)
                await Task.Delay(delay);
            // Every ArkParse install/update path ends here.
            EndArkParseInstall();
            if (MainWindow._mainWindow != null)
            {
                ret = MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                {
                    MainWindow._mainWindow.HidePopup();
                    // Notify that ArkParse update has been completed (CLI case).
                    MainWindow.ArkParseUpdateCompleted = true;
                });
            }
            return ret;
        }

        private static async Task<bool> CloseArkParserExtractPopup(JsonExportProfile? jep, int delay = 1250)
        {
            bool ret = false;
            if (SettingsPage._page != null)
            {
                if (delay > 0)
                    await Task.Delay(delay);
                ret = SettingsPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                {
                    SettingsPage._page.JsonExportProfileSelected(jep);
#pragma warning disable CS8625
                    SettingsPage._page.CloseArkParserPopupClicked(null, null);
#pragma warning restore CS8625
                });
            }
            return ret;
        }

        private static void AddJsonExportProfileToSettingsPageDropDown(JsonExportProfile? jep)
        {
            if (jep == null)
                return;

            if (SettingsPage._page != null)
            {
                SettingsPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    SettingsPage._page.InitDatagrid(false, false);
                });
            }
        }

        public static async void InstallPythonVenv()
        {
            try
            {
                if (IsExtractionRunning)
                {
                    NotifyBusy();
                    return;
                }
                ShowInstallingPopup($"{ASILang.Get("ArkParseInstallingUpdating")} {ASILang.Get("PleaseWait")}");

                if (SettingsPage.UsePythonVenv())
                    AddPythonVenvSetup();
                await SetupPythonVenv();
            }
            catch (Exception ex)
            {
                // async void: an exception would otherwise be lost (or crash the app). Log it instead.
                Logger.Instance.Log($"Exception caught in PythonManager.InstallPythonVenv. Exception=[{ex}]", Logger.LogLevel.ERROR);
                HideInstallingPopup(0).FireAndForget(nameof(HideInstallingPopup)); // Also releases the install lock.
            }
        }

        private static async Task DoRunArkParse(long extractionId, string arkParseRunnerPath, bool checkForDistUtilsPrecedenceError, JsonExportProfile? jep, List<KeyValuePair<JsonExportProfile, bool>>? extractions, Action<List<KeyValuePair<JsonExportProfile, bool>>>? callback = null)
        {
            AddDetailsToInstallingPopup(ASILang.Get("ArkParseExtractingJsonData"));
            Process? process = null;
            int exitCode = -1;
            try
            {
                process = new Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = arkParseRunnerPath;
                process.StartInfo.WorkingDirectory = Utils.GetDataDir();
                process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                process.OutputDataReceived += ShowProcessOutput;
                process.ErrorDataReceived += ShowProcessError;
                ResetProcessLogs();
                process.Start();
                _runningArkParseProcess = process;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                AddDetailsToInstallingPopup($"{ASILang.Get("ArkParseRunError")} Exception=[{ex}]", false, true);
            }
            finally
            {
                try
                {
                    if (process != null)
                        exitCode = process.ExitCode;
                }
                catch { exitCode = -1; }
                if (process != null)
                {
                    _runningArkParseProcess = null;
                    process.Dispose();
                    process = null;
                }
                if (exitCode == 0)
                    AddDetailsToInstallingPopup(ASILang.Get("ArkParseJsonExtractSuccess"), false, false, true);
                else
                    AddDetailsToInstallingPopup(ASILang.Get("ArkParseJsonExtractFail"), false, true);
                Logger.Instance.Log($"{ASILang.Get("PythonExitCode")}: {exitCode.ToString(CultureInfo.InvariantCulture)}", Logger.LogLevel.INFO);

                if (checkForDistUtilsPrecedenceError && exitCode != 0 && HasDistUtilsPrecedenceError())
                {
                    // Handle distutils-precedence.pth error.
                    HandleDistUtilsPrecedenceError();
                    // Now re-run ArkParse.
                    await DoRunArkParse(extractionId, arkParseRunnerPath, false, jep, extractions, callback);
                }
                else
                {
                    if (exitCode != 0)
                    {
                        // Let the CLI (-ExtractPreset) know that this extraction has failed.
                        MainWindow.PresetExtractionFailed = true;
                        // Remove the profile of the failed extraction (its JSON files are missing or incomplete).
                        DiscardFailedExtraction(jep, $"exit code {exitCode.ToString(CultureInfo.InvariantCulture)}");
                    }

                    bool hidden = false;
                    int cnt = 0;
                    while (!hidden && cnt < 10)
                    {
                        hidden = await HideInstallingPopup();
                        cnt++;
                    }

                    if (exitCode == 0)
                    {
                        hidden = false;
                        cnt = 0;
                        while (!hidden && cnt < 10)
                        {
                            hidden = await CloseArkParserExtractPopup(jep);
                            cnt++;
                        }
                    }

                    // This extraction is over (popups closed): release the lock BEFORE the callback, which starts the next extraction of the preset.
                    EndExtraction(extractionId);

                    if (callback != null && extractions != null)
                        callback(extractions);
                }
            }
        }

        public static async Task<bool> RunArkParse(string saveFilePath, string mapName, string? extractName, bool extractDinos, bool extractPlayerPawns, bool extractItems, bool extractStructures, bool extractPlayers, bool extractTribes, bool fastExtract, List<KeyValuePair<JsonExportProfile, bool>>? extractions, Action<List<KeyValuePair<JsonExportProfile, bool>>>? callback = null)
        {
            // Only one extraction at a time (ex: the "Extract" button can still be triggered with the keyboard while the popup is shown),
            // and never during an ArkParse install/update. A second extraction would rewrite arkparse_runner.bat while cmd.exe is reading it.
            long extractionId = TryBeginExtraction();
            if (extractionId == 0)
                return NotifyBusy();
            bool started = false;
            try
            {
                started = await RunArkParseCore(extractionId, saveFilePath, mapName, extractName, extractDinos, extractPlayerPawns, extractItems, extractStructures, extractPlayers, extractTribes, fastExtract, extractions, callback);
                return started;
            }
            finally
            {
                // When the extraction has started, the lock is released by DoRunArkParse once the extraction is over.
                if (!started)
                    EndExtraction(extractionId);
            }
        }

        private static async Task<bool> RunArkParseCore(long extractionId, string saveFilePath, string mapName, string? extractName, bool extractDinos, bool extractPlayerPawns, bool extractItems, bool extractStructures, bool extractPlayers, bool extractTribes, bool fastExtract, List<KeyValuePair<JsonExportProfile, bool>>? extractions, Action<List<KeyValuePair<JsonExportProfile, bool>>>? callback = null)
        {
            if (SettingsPage.UsePythonVenv())
            {
                if (string.IsNullOrWhiteSpace(SettingsPage._pythonExePath) || !File.Exists(SettingsPage._pythonExePath) || !File.Exists(Utils.PythonFilePathFromVenv()))
                {
                    Logger.Instance.Log($"{ASILang.Get("PythonExeNotSet")} {ASILang.Get("CheckASISettings")}", Logger.LogLevel.WARNING);
                    MainWindow.ShowToast($"{ASILang.Get("PythonExeNotSet")} {ASILang.Get("CheckSettings")}", BackgroundColor.WARNING);
                    return false;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(SettingsPage._pythonExePath) || !File.Exists(SettingsPage._pythonExePath))
                {
                    Logger.Instance.Log($"{ASILang.Get("PythonExeNotSet")} {ASILang.Get("CheckASISettings")}", Logger.LogLevel.WARNING);
                    MainWindow.ShowToast($"{ASILang.Get("PythonExeNotSet")} {ASILang.Get("CheckSettings")}", BackgroundColor.WARNING);
                    return false;
                }
            }

            ShowInstallingPopup($"{ASILang.Get("ArkParseExtractingJsonData")} {ASILang.Get("PleaseWait")}");

            JsonExportProfile? jep = SettingsPage.AddNewJsonExportProfile(saveFilePath, mapName, extractName, extractDinos, extractPlayerPawns, extractItems, extractStructures, extractPlayers, extractTribes, fastExtract);
            if (jep == null)
            {
                Logger.Instance.Log(ASILang.Get("ArkParseJsonExportProfileCreationFailed"), Logger.LogLevel.ERROR);
                HideInstallingPopup().FireAndForget(nameof(HideInstallingPopup)); // The popup used to stay open in this case.
                return false;
            }
            AddJsonExportProfileToSettingsPageDropDown(jep);

            if (SettingsPage._debugLogging != null && SettingsPage._debugLogging.HasValue && SettingsPage._debugLogging.Value)
                AddPythonTestFile();
            string arkParseRunnerPath = Utils.ArkParseRunnerFilePath();
            // Only run the runner if it has just been written for THIS extraction (never a runner left by a previous one).
            if (!AddArkParseRunner(jep) || string.IsNullOrWhiteSpace(arkParseRunnerPath) || !File.Exists(arkParseRunnerPath))
            {
                Logger.Instance.Log($"{ASILang.Get("ArkParseAddRunnerError")} (E)", Logger.LogLevel.ERROR);
                DiscardFailedExtraction(jep, "the extraction script could not be generated");
                HideInstallingPopup().FireAndForget(nameof(HideInstallingPopup));
                return false;
            }

            Task.Run(async () =>
            {
                try { await DoRunArkParse(extractionId, arkParseRunnerPath, true, jep, extractions, callback); }
                finally { EndExtraction(extractionId); } // Safety net (ex: unexpected exception). Does nothing if already released.
            }).FireAndForget(nameof(DoRunArkParse));
            return true;
        }
#pragma warning restore CS1998
    }
}
