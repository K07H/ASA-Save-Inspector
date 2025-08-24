using ASA_Save_Inspector.Pages;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ASA_Save_Inspector
{
    class PythonManager
    {
        private static readonly HttpClient _client = new HttpClient();

        public static void InitHttpClient()
        {
            if (_client != null)
                _client.DefaultRequestHeaders.UserAgent.ParseAdd($"ASASaveInspector/{Utils.GetVersionStr()}");
        }

        public static List<string>? GetPythonExePaths()
        {
            List<string>? paths = Utils.GetPathsFromEnvironmentVariables();
            if (paths == null || paths.Count <= 0)
                return null;
            List<string> result = new List<string>();
            foreach (string path in paths)
                if (path.Contains("Python3", StringComparison.InvariantCultureIgnoreCase) && Directory.Exists(path))
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
            return result;
        }

        public static bool IsArkParsePresent() => File.Exists(Utils.ArkParseJsonApiFilePath());

        public static async Task<bool> DownloadArkParse()
        {
            try
            {
                Utils.EnsureDataFolderExist();
                string fileToDownload = Utils.ArkParseArchiveUrl;
                string filePath = Utils.ArkParseArchiveFilePath();
                using var downloadStream = await _client.GetStreamAsync(fileToDownload);
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                await downloadStream.CopyToAsync(fileStream);
                await fileStream.FlushAsync();
                fileStream.Close();
                downloadStream.Close();
                if (File.Exists(filePath))
                    return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in DownloadArkParse. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }
            return false;
        }

        private static string? GetLocalArkParseVersion()
        {
            string arkParseProjectFilePath = Utils.ArkParseProjectFilePath();
            if (!File.Exists(arkParseProjectFilePath))
            {
                Logger.Instance.Log("Could not get ArkParse version (pyproject.toml file not found on disk).", Logger.LogLevel.WARNING);
                return null;
            }
            string[] lines = File.ReadAllLines(arkParseProjectFilePath);
            if (lines == null || lines.Length <= 0)
            {
                Logger.Instance.Log("Could not get ArkParse version (local pyproject.toml is empty).", Logger.LogLevel.WARNING);
                return null;
            }
            foreach (string line in lines)
                if (!string.IsNullOrWhiteSpace(line) && line.StartsWith("version = \"", StringComparison.InvariantCulture))
                {
                    string[] splitted = line.Split('"', StringSplitOptions.RemoveEmptyEntries);
                    if (splitted != null && splitted.Length > 1)
                        return splitted[1];
                    break;
                }
            Logger.Instance.Log("Could not get ArkParse version (could not find line starting with 'version = \"' in local pyproject.toml).", Logger.LogLevel.WARNING);
            return null;
        }

        private static async Task<string?> GetRepoArkParseVersion()
        {
            string projectFileContent = await _client.GetStringAsync(Utils.ArkParseProjectUrl);
            if (string.IsNullOrWhiteSpace(projectFileContent))
            {
                Logger.Instance.Log("Could not get ArkParse version (pyproject.toml file not found in repository).", Logger.LogLevel.WARNING);
                return null;
            }
            string[] lines = projectFileContent.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines == null || lines.Length <= 0)
            {
                Logger.Instance.Log("Could not get ArkParse version (pyproject.toml from repository is empty).", Logger.LogLevel.WARNING);
                return null;
            }
            foreach (string line in lines)
                if (!string.IsNullOrWhiteSpace(line) && line.StartsWith("version = \"", StringComparison.InvariantCulture))
                {
                    string[] splitted = line.Split('"', StringSplitOptions.RemoveEmptyEntries);
                    if (splitted != null && splitted.Length > 1)
                        return splitted[1];
                    break;
                }
            Logger.Instance.Log("Could not get ArkParse version (could not find line starting with 'version = \"' in pyproject.toml from repository).", Logger.LogLevel.WARNING);
            return null;
        }

        public static async Task<bool> DownloadAndExtractArkParse()
        {
            if (IsArkParsePresent())
            {
                string? localVersion = GetLocalArkParseVersion();
                string? repoVersion = await GetRepoArkParseVersion();
                if (string.Compare(localVersion, repoVersion, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    AddDetailsToInstallingPopup("ArkParse is up to date.", false, false, true);
                    return false;
                }
                AddDetailsToInstallingPopup("Updating ArkParse...");
            }
            else
                AddDetailsToInstallingPopup("Downloading ArkParse archive...");

            bool downloaded = await DownloadArkParse();
            if (!downloaded)
            {
                AddDetailsToInstallingPopup("Downloading ArkParse failed.", false, true);
                return false;
            }
            AddDetailsToInstallingPopup("ArkParse archive successfully downloaded.", false, false, true);

            string filePath = Utils.ArkParseArchiveFilePath();
            try
            {
                AddDetailsToInstallingPopup("Extracting ArkParse archive...");
                ZipFile.ExtractToDirectory(filePath, Utils.GetDataDir(), true);
                if (File.Exists(filePath))
                    try { File.Delete(filePath); }
                    catch { }
                if (IsArkParsePresent())
                {
                    AddDetailsToInstallingPopup("ArkParse archive successfully extracted.", false, false, true);
                    return true;
                }
                else
                {
                    AddDetailsToInstallingPopup("ArkParse archive extraction failed.", false, true);
                    return false;
                }
            }
            catch (Exception ex)
            {
                AddDetailsToInstallingPopup($"ERROR: Exception caught in DownloadAndExtractArkParse. Exception=[{ex}]", false, true);
            }

            if (File.Exists(filePath))
                try { File.Delete(filePath); }
                catch { }
            return false;
        }

#pragma warning disable CS1998, CS4014
        public static async Task<bool> SetupPythonVenv()
        {
            if (Directory.Exists(Utils.PythonVenvFolder()))
            {
                Logger.Instance.Log("Python's virtual environment is already setup.");
                return await ActivatePythonVenv();
            }

            AddDetailsToInstallingPopup("Setting up virtual environment for Python...");
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
                    process.OutputDataReceived += ShowProcessOutput;
                    process.ErrorDataReceived += ShowProcessError;
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    AddDetailsToInstallingPopup($"Exception caught in SetupPythonVenv. Exception=[{ex}]", false, true);
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
                        AddDetailsToInstallingPopup("Python's virtual environment has been setup.", false, false, true);
                    else
                        AddDetailsToInstallingPopup("Python's virtual environment setup failed.", false, true);
                    Logger.Instance.Log($"Python exit code: {exitCode}", Logger.LogLevel.INFO);

                    await ActivatePythonVenv();
                }
            });

            return true;
        }

        public static async Task<bool> UpdateOrInstallArkParse()
        {

            if (!File.Exists(Utils.PythonFilePathFromVenv()))
            {
                HideInstallingPopup();
                return false;
            }

            AddArkParseSetup();
            if (!(await DownloadAndExtractArkParse()))
            {
                HideInstallingPopup();
                return false;
            }
            if (!Directory.Exists(Utils.ArkParseFolder()))
            {
                HideInstallingPopup();
                return false;
            }
            string arkParseSetupPath = Utils.ArkParseSetupFilePath();
            if (string.IsNullOrWhiteSpace(arkParseSetupPath) || !File.Exists(arkParseSetupPath))
            {
                HideInstallingPopup();
                return false;
            }

            AddDetailsToInstallingPopup("Installing ArkParse...");
            Task.Run(() =>
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
                    process.StartInfo.FileName = arkParseSetupPath;
                    process.StartInfo.WorkingDirectory = Utils.ArkParseFolder();
                    process.OutputDataReceived += ShowProcessOutput;
                    process.ErrorDataReceived += ShowProcessError;
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    AddDetailsToInstallingPopup($"ERROR: Exception caught in InstallArkParse. Exception=[{ex}]", false, true);
                    //Logger.Instance.Log($"Exception caught in InstallArkParse. Exception=[{ex}]", Logger.LogLevel.ERROR);
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
                        AddDetailsToInstallingPopup("ArkParse successfully installed", false, false, true);
                    else
                        AddDetailsToInstallingPopup("ArkParse installation failed.", false, true);
                    Logger.Instance.Log($"Python exit code: {exitCode}", Logger.LogLevel.INFO);
                    HideInstallingPopup();
                }
            });
            return true;
        }

        private static bool _pythonVenvActivated = false;
        public static async Task<bool> ActivatePythonVenv()
        {
            if (!File.Exists(Utils.ActivatePythonVenvFilePath()))
            {
                AddDetailsToInstallingPopup($"Python's virtual environment activation script not found at {Utils.ActivatePythonVenvFilePath()}.", false, true);
                return false;
            }

            AddDetailsToInstallingPopup("Activating Python's virtual environment...");
            _pythonVenvDeactivated = false;
            _pythonVenvActivated = true;
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
                    process.StartInfo.FileName = Utils.ActivatePythonVenvFilePath();
                    process.StartInfo.WorkingDirectory = Utils.GetDataDir();
                    process.OutputDataReceived += ShowProcessOutput;
                    process.ErrorDataReceived += ShowProcessError;
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    AddDetailsToInstallingPopup($"Exception caught in ActivatePythonVenv. Exception=[{ex}]", false, true);
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
                        AddDetailsToInstallingPopup("Python's virtual environment has been activated.", false, false, true);
                    else
                        AddDetailsToInstallingPopup("Python's virtual environment activation failed.", false, true);
                    Logger.Instance.Log($"Python exit code: {exitCode}", Logger.LogLevel.INFO);

                    await UpdateOrInstallArkParse();
                }
            });

            return true;
        }

        private static bool _pythonVenvDeactivated = false;
        public static async Task<bool> DeactivatePythonVenv()
        {
            if (!_pythonVenvActivated || _pythonVenvDeactivated)
                return false;
            if (!File.Exists(Utils.DeactivatePythonVenvFilePath()))
            {
                Logger.Instance.Log($"Python's virtual environment deactivation script not found at {Utils.DeactivatePythonVenvFilePath()}.", Logger.LogLevel.ERROR);
                return false;
            }

            Logger.Instance.Log("Deactivating Python's virtual environment...");
            _pythonVenvDeactivated = true;
            _pythonVenvActivated = false;
            var t = Task.Run(() =>
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
                    process.StartInfo.FileName = Utils.DeactivatePythonVenvFilePath();
                    process.StartInfo.WorkingDirectory = Utils.GetDataDir();
                    process.OutputDataReceived += ShowProcessOutput;
                    process.ErrorDataReceived += ShowProcessError;
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Exception caught in DeactivatePythonVenv. Exception=[{ex}]", Logger.LogLevel.ERROR);
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
                        Logger.Instance.Log("Python's virtual environment has been deactivated.");
                    else
                        Logger.Instance.Log("Python's virtual environment deactivation failed.", Logger.LogLevel.ERROR);
                    Logger.Instance.Log($"Python exit code: {exitCode}", Logger.LogLevel.INFO);
                }
            });
            if (t != null)
                t.Wait(4000);

            return true;
        }
#pragma warning restore CS1998, CS4014

        public static bool CreateAsiExportAllFile()
        {
            string asiExportAllOrigPath = Utils.AsiExportAllOrigFilePath();
            if (!File.Exists(asiExportAllOrigPath))
            {
                Logger.Instance.Log("Could not find file asi_export_all.py in Assets folder.", Logger.LogLevel.ERROR);
                return false;
            }

            string asiExportAllPath = Utils.AsiExportAllFilePath();
            try
            {
                Utils.EnsureDataFolderExist();
                string asiExportAllContent = File.ReadAllText(asiExportAllOrigPath, Encoding.UTF8);
                File.WriteAllText(asiExportAllPath, asiExportAllContent, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in CreateAsiExtractAllFile. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }

            return File.Exists(asiExportAllPath);
        }

        public static bool AddPythonVenvSetup()
        {
            if (File.Exists(Utils.PythonFilePathFromVenv()))
                return false;

            Utils.EnsureDataFolderExist();
            string pythonVenvSetupPath = Utils.PythonVenvSetupFilePath();
            string pythonVenvSetupContent = string.Format(
@"
""{0}"" -m venv python_venv
", SettingsPage._pythonExePath);

            try
            {
                File.WriteAllText(pythonVenvSetupPath, pythonVenvSetupContent, Encoding.ASCII);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in AddPythonVenvSetup. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }

            return File.Exists(pythonVenvSetupPath);
        }

        public static bool AddArkParseSetup()
        {
            if (!File.Exists(Utils.PythonFilePathFromVenv()))
                return false;

            Utils.EnsureDataFolderExist();
            string arkParseSetupPath = Utils.ArkParseSetupFilePath();
            string arkParseSetupContent = string.Format(
@"
""{0}"" -m ensurepip --upgrade
""{0}"" -m pip install --upgrade pip setuptools wheel
""{0}"" -m pip install pytz
""{0}"" -m pip install --upgrade pytz
""{0}"" -m pip install rcon
""{0}"" -m pip install --upgrade rcon
""{0}"" -m pip install numpy
""{0}"" -m pip install --upgrade numpy
""{0}"" -m pip install -e .
", Utils.PythonFilePathFromVenv());

            try
            {
                File.WriteAllText(arkParseSetupPath, arkParseSetupContent, Encoding.ASCII);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"Exception caught in AddArkParseSetup. Exception=[{ex}]", Logger.LogLevel.ERROR);
            }

            return File.Exists(arkParseSetupPath);
        }

        public static bool AddArkParseRunner(JsonExportProfile? jep)
        {
            if (!File.Exists(Utils.PythonFilePathFromVenv()))
                return false;
            if (string.IsNullOrWhiteSpace(SettingsPage._asaSaveFilePath))
                return false;
            if (jep == null)
                return false;
            string asiExportAllPath = Utils.AsiExportAllFilePath();
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
                    Logger.Instance.Log($"Exception caught in AddArkParseRunner. Exception=[{ex}]", Logger.LogLevel.ERROR);
                    return false;
                }
            }

            string finalExportFolderPath = jep.GetExportFolderName();
            if (!Directory.Exists(finalExportFolderPath))
                finalExportFolderPath = Path.Combine(exportFolderPath, jep.GetExportFolderName());
            if (!Directory.Exists(finalExportFolderPath))
            {
                try
                {
                    Directory.CreateDirectory(finalExportFolderPath);
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Exception caught in AddArkParseRunner. Exception=[{ex}]", Logger.LogLevel.ERROR);
                    return false;
                }
            }

            string arkParseRunnerPath = Utils.ArkParseRunnerFilePath();
            string arkParseRunnerContent = string.Format(
@"
""{0}"" ""{1}"" ""{2}"" ""{3}"" {4} {5} {6} {7} {8} {9}
", Utils.PythonFilePathFromVenv(), asiExportAllPath, SettingsPage._asaSaveFilePath, finalExportFolderPath, (jep.ExtractedDinos ? "1" : "0"), (jep.ExtractedPlayerPawns ? "1" : "0"), (jep.ExtractedItems ? "1" : "0"), (jep.ExtractedStructures ? "1" : "0"), (jep.ExtractedPlayers ? "1" : "0"), (jep.ExtractedTribes ? "1" : "0"));

            try
            {
                File.WriteAllText(arkParseRunnerPath, arkParseRunnerContent, Encoding.ASCII);
            }
            catch (Exception exb)
            {
                Logger.Instance.Log($"Exception caught in AddArkParseRunner. Exception=[{exb}]", Logger.LogLevel.ERROR);
            }

            return File.Exists(arkParseRunnerPath);
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

        private static string StripPythonColors(string str)
        {
            for (int i = 0; i < 7; i++)
                str = str.Replace(_pythonColorCodes[i], "", StringComparison.InvariantCulture);
            return str.Replace("\e", "");
        }

        private static void ShowProcessError(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                string str = StripPythonColors(e.Data);
                //Logger.Instance.Log(str, Logger.LogLevel.ERROR);
                AddDetailsToInstallingPopup(str, false, true);
            }
        }

        private static void ShowProcessOutput(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                string str = StripPythonColors(e.Data);
                //Logger.Instance.Log(str, Logger.LogLevel.INFO);
                AddDetailsToInstallingPopup(str);
            }
        }

#pragma warning disable CS1998, CS4014
        private static Brush _errorColor = new SolidColorBrush(Colors.Red);
        private static Brush _warningColor = new SolidColorBrush(Colors.Orange);
        private static Brush _successColor = new SolidColorBrush(Colors.Green);
        private static async void AddDetailsToInstallingPopup(string txt, bool isWarning = false, bool isError = false, bool isSuccess = false)
        {
            if (MainWindow._mainWindow != null)
                MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, async () =>
                {
                    Logger.Instance.Log(txt, (isError ? Logger.LogLevel.ERROR : (isWarning ? Logger.LogLevel.WARNING : Logger.LogLevel.INFO)));
                    MainWindow._mainWindow.AddTextToPopupDetails(txt, (isError ? _errorColor : (isWarning ? _warningColor : (isSuccess ? _successColor : null))));
                });
        }

        private static async void ShowInstallingPopup(string title)
        {
            if (MainWindow._mainWindow != null)
                MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, async () =>
                {
                    MainWindow._mainWindow.ShowPopup(title);
                });
        }

        private static async Task<bool> HideInstallingPopup(int delay = 1000)
        {
            bool ret = false;
            if (MainWindow._mainWindow != null)
            {
                if (delay > 0)
                    await Task.Delay(delay);
                ret = MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                {
                    MainWindow._mainWindow.HidePopup();
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

        private static async void AddJsonExportProfileToSettingsPageDropDown(JsonExportProfile? jep)
        {
            if (jep == null)
                return;

            if (SettingsPage._page != null)
            {
                bool isQueued = SettingsPage._page.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                {
                    SettingsPage._page.AddJsonExportProfileToDropDown(jep);
                });
            }
        }

        public static async void InstallArkParse()
        {
            ShowInstallingPopup("Installing/updating ArkParse, please wait...");

            AddPythonVenvSetup();
            await SetupPythonVenv();
        }

        public static async Task<bool> RunArkParse(bool extractDinos, bool extractPlayerPawns, bool extractItems, bool extractStructures, bool extractPlayers, bool extractTribes)
        {
            if (string.IsNullOrWhiteSpace(SettingsPage._pythonExePath) || !File.Exists(SettingsPage._pythonExePath) || !File.Exists(Utils.PythonFilePathFromVenv()))
            {
                Logger.Instance.Log("Python executable not set, check ASA Save Inspector settings.", Logger.LogLevel.WARNING);
                MainWindow.ShowToast("Python exe not set, check settings", BackgroundColor.WARNING);
                return false;
            }

            ShowInstallingPopup("Extracting JSON Data, please wait...");

            JsonExportProfile? jep = SettingsPage.AddNewJsonExportProfile(SettingsPage._asaSaveFilePath, SettingsPage._mapName, extractDinos, extractPlayerPawns, extractItems, extractStructures, extractPlayers, extractTribes);
            if (jep == null)
            {
                Logger.Instance.Log("Failed to create new JSON export profile.", Logger.LogLevel.ERROR);
                return false;
            }
            AddJsonExportProfileToSettingsPageDropDown(jep);

            AddArkParseRunner(jep);
            string arkParseRunnerPath = Utils.ArkParseRunnerFilePath();
            if (string.IsNullOrWhiteSpace(arkParseRunnerPath) || !File.Exists(arkParseRunnerPath))
            {
                HideInstallingPopup();
                return false;
            }

            AddDetailsToInstallingPopup("Extracting JSON Data...");
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
                    process.StartInfo.FileName = arkParseRunnerPath;
                    process.StartInfo.WorkingDirectory = Utils.GetDataDir();
                    process.OutputDataReceived += ShowProcessOutput;
                    process.ErrorDataReceived += ShowProcessError;
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    AddDetailsToInstallingPopup($"ERROR: Exception caught in RunArkParse. Exception=[{ex}]", false, true);
                    //Logger.Instance.Log($"Exception caught in RunArkParse. Exception=[{ex}]", Logger.LogLevel.ERROR);
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
                        AddDetailsToInstallingPopup("JSON Data successfully extracted.", false, false, true);
                    else
                        AddDetailsToInstallingPopup("JSON Data extraction failed.", false, true);
                    Logger.Instance.Log($"Python exit code: {exitCode}", Logger.LogLevel.INFO);

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
                }
            });

            return true;
        }
#pragma warning restore CS1998, CS4014
    }
}
