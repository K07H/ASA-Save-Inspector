using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ASA_Save_Inspector.Pages;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

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
                    if (Directory.Exists(path) && (path.Contains("Python3", StringComparison.InvariantCultureIgnoreCase) || path.Contains("Python", StringComparison.InvariantCultureIgnoreCase)))
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
                Logger.Instance.Log($"{ASILang.Get("ArkParseDownloadingError")} Exception=[{ex}]", Logger.LogLevel.ERROR);
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

        public static async Task<bool> DownloadAndExtractArkParse()
        {
            if (IsArkParsePresent())
            {
                string? localVersion = GetLocalArkParseVersion();
                string? repoVersion = await GetRepoArkParseVersion();
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
                ZipFile.ExtractToDirectory(filePath, Utils.GetDataDir(), true);
                if (File.Exists(filePath))
                    try { File.Delete(filePath); }
                    catch { }
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
                catch { }
            return false;
        }

#pragma warning disable CS1998, CS4014
        public static async Task<bool> SetupPythonVenv()
        {
            if (!SettingsPage.UsePythonVenv())
                return await ActivatePythonVenv();

            if (Directory.Exists(Utils.PythonVenvFolder()))
            {
                Logger.Instance.Log(ASILang.Get("PythonVenvAlreadySetup"));
                return await ActivatePythonVenv();
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
                    process.OutputDataReceived += ShowProcessOutput;
                    process.ErrorDataReceived += ShowProcessError;
                    _processOutput.Clear();
                    _processErrors.Clear();
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

                    await ActivatePythonVenv();
                }
            });

            return true;
        }

        private static void HandleDistUtilsPrecedenceError()
        {
            string distUtilsPrecedenceFilePath = Path.Combine(Utils.PythonVenvFolder(), "Lib", "site-packages", "distutils-precedence.pth");
            Logger.Instance.Log($"{ASILang.Get("PythonDistUtilsPrecedenceError").Replace("#FILEPATH#", $"\"{distUtilsPrecedenceFilePath}\"", StringComparison.InvariantCulture)}", Logger.LogLevel.INFO);
            try
            {
                if (File.Exists(distUtilsPrecedenceFilePath))
                    File.Delete(distUtilsPrecedenceFilePath);
            }
            catch (Exception ex)
            {
                Logger.Instance.Log($"{ASILang.Get("DeleteFileAtFailed").Replace("#FILEPATH#", $"\"{distUtilsPrecedenceFilePath}\"", StringComparison.InvariantCulture)} Exception=[{ex}]", Logger.LogLevel.WARNING);
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
                process.OutputDataReceived += ShowProcessOutput;
                process.ErrorDataReceived += ShowProcessError;
                _processOutput.Clear();
                _processErrors.Clear();
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
                    catch { }
                    process = null;
                }
                if (exitCode == 0)
                {
                    AddDetailsToInstallingPopup(ASILang.Get("ArkParseInstallSuccess"), false, false, true);
                    if (RequiresArkParseReinstall())
                        try { File.Delete(Utils.ArkParseRequiresReinstallFilePath()); }
                        catch (Exception exb) { Logger.Instance.Log($"Exception caught while deleting file at \"{Utils.ArkParseRequiresReinstallFilePath()}\". Exception=[{exb}]", Logger.LogLevel.ERROR); }
                }
                else
                    AddDetailsToInstallingPopup(ASILang.Get("ArkParseInstallFail"), false, true);
                Logger.Instance.Log($"{ASILang.Get("PythonExitCode")}: {exitCode.ToString(CultureInfo.InvariantCulture)}", Logger.LogLevel.INFO);
                if (checkForDistUtilsPrecedenceError && exitCode != 0 && _processErrors.Where(s => s.Contains("Error processing line 1 of ", StringComparison.InvariantCultureIgnoreCase) && s.Contains("distutils-precedence.pth", StringComparison.InvariantCultureIgnoreCase)).Count() >= 1)
                {
                    // Handle distutils-precedence.pth error.
                    HandleDistUtilsPrecedenceError();
                    // Now re-run ArkParse update/install.
                    DoUpdateOrInstallArkParse(arkParseSetupPath, false);
                }
                else
                    HideInstallingPopup();
            }
        }

        public static bool RequiresArkParseReinstall() => File.Exists(Utils.ArkParseRequiresReinstallFilePath());

        public static async Task<bool> UpdateOrInstallArkParse()
        {
            if (SettingsPage._debugLogging != null && SettingsPage._debugLogging.HasValue && SettingsPage._debugLogging.Value)
                Utils.LogASIDataFolder();

            if (SettingsPage.UsePythonVenv())
            {
                if (!File.Exists(Utils.PythonFilePathFromVenv()))
                {
                    HideInstallingPopup();
                    return false;
                }
            }
            else
            {
                if (!File.Exists(SettingsPage._pythonExePath))
                {
                    HideInstallingPopup();
                    return false;
                }
            }

            if (SettingsPage._debugLogging != null && SettingsPage._debugLogging.HasValue && SettingsPage._debugLogging.Value)
                AddPythonTestFile();

            AddArkParseSetup();
            if (!(await DownloadAndExtractArkParse()) && !RequiresArkParseReinstall())
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

            Task.Run(() => DoUpdateOrInstallArkParse(arkParseSetupPath, true));
            return true;
        }

        private static bool _pythonVenvActivated = false;
        public static async Task<bool> ActivatePythonVenv()
        {
            if (!SettingsPage.UsePythonVenv())
            {
                await UpdateOrInstallArkParse();
                return true;
            }

            if (!File.Exists(Utils.ActivatePythonVenvFilePath()))
            {
                AddDetailsToInstallingPopup($"{ASILang.Get("PythonVenvActivationScriptNotFound").Replace("#FILEPATH#", $"\"{Utils.ActivatePythonVenvFilePath()}\"", StringComparison.InvariantCulture)}", false, true);
                return false;
            }

            AddDetailsToInstallingPopup(ASILang.Get("PythonVenvActivate"));
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
                    _processOutput.Clear();
                    _processErrors.Clear();
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    AddDetailsToInstallingPopup($"{ASILang.Get("PythonVenvActivateError")} Exception=[{ex}]", false, true);
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
                        AddDetailsToInstallingPopup(ASILang.Get("PythonVenvActivateSuccess"), false, false, true);
                    else
                        AddDetailsToInstallingPopup(ASILang.Get("PythonVenvActivateFail"), false, true);
                    Logger.Instance.Log($"{ASILang.Get("PythonExitCode")}: {exitCode.ToString(CultureInfo.InvariantCulture)}", Logger.LogLevel.INFO);

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
                Logger.Instance.Log($"{ASILang.Get("PythonVenvDeactivationScriptNotFound").Replace("#FILEPATH#", $"\"{Utils.DeactivatePythonVenvFilePath()}\"", StringComparison.InvariantCulture)}", Logger.LogLevel.ERROR);
                return false;
            }

            Logger.Instance.Log(ASILang.Get("PythonVenvDeactivate"));
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
                    _processOutput.Clear();
                    _processErrors.Clear();
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"{ASILang.Get("PythonVenvDeactivateError")} Exception=[{ex}]", Logger.LogLevel.ERROR);
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
                        Logger.Instance.Log(ASILang.Get("PythonVenvDeactivateSuccess"));
                    else
                        Logger.Instance.Log(ASILang.Get("PythonVenvDeactivateFail"), Logger.LogLevel.ERROR);
                    Logger.Instance.Log($"{ASILang.Get("PythonExitCode")}: {exitCode.ToString(CultureInfo.InvariantCulture)}", Logger.LogLevel.INFO);
                }
            });
            if (t != null)
                t.Wait(4000);

            return true;
        }
#pragma warning restore CS1998, CS4014

        public static bool CreateAsiExportScriptFile(string? asiExportFilePathSource, string? asiExportFilePathDest)
        {
            if (string.IsNullOrEmpty(asiExportFilePathSource))
            {
                Logger.Instance.Log(ASILang.Get("ASIExportScript_BadSourceFilePath"), Logger.LogLevel.ERROR);
                return false;
            }
            if (!File.Exists(asiExportFilePathSource))
            {
                Logger.Instance.Log($"{ASILang.Get("ASIExportScript_NotFound").Replace("#FILEPATH#", $"\"{Path.GetFileName(asiExportFilePathSource)}\"", StringComparison.InvariantCulture)}", Logger.LogLevel.ERROR);
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
", SettingsPage._pythonExePath);

            try
            {
                File.WriteAllText(pythonVenvSetupPath, pythonVenvSetupContent, Encoding.ASCII);
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
            string? arkParseSetupContent = null;
            if (SettingsPage._debugLogging != null && SettingsPage._debugLogging.HasValue && SettingsPage._debugLogging.Value)
                arkParseSetupContent = string.Format(
@"
{0}
echo PATH environment variable
echo %PATH%
echo PYTHONPATH environment variable
echo %PYTHONPATH%
""{1}"" ""{2}""
""{1}"" -m ensurepip --upgrade
""{1}"" -m pip install --upgrade pip
""{1}"" -m pip --version
""{1}"" -m pip show pip
""{1}"" -m pip list -v
""{1}"" -m pip freeze --path ""{0}""
",
SettingsPage.UsePythonVenv() ? $"set \"PYTHONPATH={Path.Combine(Utils.PythonVenvFolder(), "Lib", "site-packages")}\"" : string.Empty,
SettingsPage.UsePythonVenv() ? Utils.PythonFilePathFromVenv() : SettingsPage._pythonExePath,
Utils.PythonTestScriptPath());
            else
                arkParseSetupContent = string.Format(
@"
{0}
""{1}"" -m ensurepip --upgrade
""{1}"" -m pip install --upgrade pip
",
SettingsPage.UsePythonVenv() ? $"set \"PYTHONPATH={Path.Combine(Utils.PythonVenvFolder(), "Lib", "site-packages")}\"" : string.Empty,
SettingsPage.UsePythonVenv() ? Utils.PythonFilePathFromVenv() : SettingsPage._pythonExePath);

            if (string.IsNullOrWhiteSpace(arkParseSetupContent))
            {
                Logger.Instance.Log($"{ASILang.Get("ArkParseAddSetupError")} (A)", Logger.LogLevel.ERROR);
                return false;
            }

            arkParseSetupContent += string.Format(
@"
""{0}"" -m pip install setuptools
""{0}"" -m pip install --upgrade setuptools
""{0}"" -m pip install wheel
""{0}"" -m pip install --upgrade wheel
""{0}"" -m pip install rcon
""{0}"" -m pip install --upgrade rcon
""{0}"" -m pip install numpy
""{0}"" -m pip install --upgrade numpy
""{0}"" -m pip install matplotlib --prefer-binary
""{0}"" -m pip install --upgrade matplotlib --prefer-binary
""{0}"" -m pip install pandas --prefer-binary
""{0}"" -m pip install --upgrade pandas --prefer-binary
""{0}"" -m pip install -e .
",
SettingsPage.UsePythonVenv() ? Utils.PythonFilePathFromVenv() : SettingsPage._pythonExePath);

            try { File.WriteAllText(arkParseSetupPath, arkParseSetupContent, Encoding.ASCII); }
            catch (Exception ex) { Logger.Instance.Log($"{ASILang.Get("ArkParseAddSetupError")} (B) Exception=[{ex}]", Logger.LogLevel.ERROR); }

            return File.Exists(arkParseSetupPath);
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
                    Logger.Instance.Log($"{ASILang.Get("ArkParseAddRunnerError")} (B) Exception=[{ex}]", Logger.LogLevel.ERROR);
                    return false;
                }
            }

            string customBlueprintsB64 = (SettingsPage.GetCustomBlueprintsB64() ?? string.Empty);
            string arkParseRunnerPath = Utils.ArkParseRunnerFilePath();
            string? arkParseRunnerContent = null;
            if (SettingsPage._debugLogging != null && SettingsPage._debugLogging.HasValue && SettingsPage._debugLogging.Value && File.Exists(Utils.PythonTestScriptPath()))
                arkParseRunnerContent = string.Format(
@"
{0}
echo PATH environment variable
echo %PATH%
echo PYTHONPATH environment variable
echo %PYTHONPATH%
""{1}"" ""{2}""
",
SettingsPage.UsePythonVenv() ? $"set \"PYTHONPATH={Path.Combine(Utils.PythonVenvFolder(), "Lib", "site-packages")}\"" : string.Empty,
SettingsPage.UsePythonVenv() ? Utils.PythonFilePathFromVenv() : SettingsPage._pythonExePath,
Utils.PythonTestScriptPath());
            else
                arkParseRunnerContent = string.Format(
@"
{0}
",
SettingsPage.UsePythonVenv() ? $"set \"PYTHONPATH={Path.Combine(Utils.PythonVenvFolder(), "Lib", "site-packages")}\"" : string.Empty);

            if (string.IsNullOrWhiteSpace(arkParseRunnerContent) && SettingsPage.UsePythonVenv())
            {
                Logger.Instance.Log($"{ASILang.Get("ArkParseAddRunnerError")} (C)", Logger.LogLevel.ERROR);
                return false;
            }

            arkParseRunnerContent += string.Format(
@"
""{0}"" ""{1}"" ""{2}"" ""{3}"" {4} {5} {6} {7} {8} {9} {10}{11}
",
SettingsPage.UsePythonVenv() ? Utils.PythonFilePathFromVenv() : SettingsPage._pythonExePath,
asiExportAllPath,
jep.SaveFilePath,
finalExportFolderPath,
(jep.ExtractedDinos ? "1" : "0"),
(jep.ExtractedPlayerPawns ? "1" : "0"),
(jep.ExtractedItems ? "1" : "0"),
(jep.ExtractedStructures ? "1" : "0"),
(jep.ExtractedPlayers ? "1" : "0"),
(jep.ExtractedTribes ? "1" : "0"),
(SettingsPage._debugLogging != null && SettingsPage._debugLogging.HasValue && SettingsPage._debugLogging.Value ? "1" : "0"),
(customBlueprintsB64.Length > 0 ? $" {customBlueprintsB64}" : string.Empty));

            try { File.WriteAllText(arkParseRunnerPath, arkParseRunnerContent, Encoding.ASCII); }
            catch (Exception ex) { Logger.Instance.Log($"{ASILang.Get("ArkParseAddRunnerError")} (D) Exception=[{ex}]", Logger.LogLevel.ERROR); }

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

        private static List<string> _processErrors = new List<string>();
        private static void ShowProcessError(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                string str = StripPythonColors(e.Data);
                //Logger.Instance.Log(str, Logger.LogLevel.ERROR);
                _processErrors.Add(str);
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
                _processOutput.Add(str);
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
                    SettingsPage._page.InitDatagrid(false, false);
                    //SettingsPage._page.AddJsonExportProfileToDropDown(jep);
                });
            }
        }

        public static async void InstallPythonVenv()
        {
            ShowInstallingPopup($"{ASILang.Get("ArkParseInstallingUpdating")} {ASILang.Get("PleaseWait")}");

            if (SettingsPage.UsePythonVenv())
                AddPythonVenvSetup();
            await SetupPythonVenv();
        }

        private static async Task DoRunArkParse(string arkParseRunnerPath, bool checkForDistUtilsPrecedenceError, JsonExportProfile? jep, List<KeyValuePair<JsonExportProfile, bool>>? extractions, Action<List<KeyValuePair<JsonExportProfile, bool>>>? callback = null)
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
                process.OutputDataReceived += ShowProcessOutput;
                process.ErrorDataReceived += ShowProcessError;
                _processOutput.Clear();
                _processErrors.Clear();
                process.Start();
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
                    process.Dispose();
                    process = null;
                }
                if (exitCode == 0)
                    AddDetailsToInstallingPopup(ASILang.Get("ArkParseJsonExtractSuccess"), false, false, true);
                else
                    AddDetailsToInstallingPopup(ASILang.Get("ArkParseJsonExtractFail"), false, true);
                Logger.Instance.Log($"{ASILang.Get("PythonExitCode")}: {exitCode.ToString(CultureInfo.InvariantCulture)}", Logger.LogLevel.INFO);

                if (checkForDistUtilsPrecedenceError && exitCode != 0 && _processErrors.Where(s => s.Contains("Error processing line 1 of ", StringComparison.InvariantCultureIgnoreCase) && s.Contains("distutils-precedence.pth", StringComparison.InvariantCultureIgnoreCase)).Count() >= 1)
                {
                    // Handle distutils-precedence.pth error.
                    HandleDistUtilsPrecedenceError();
                    // Now re-run ArkParse.
                    DoRunArkParse(arkParseRunnerPath, false, jep, extractions, callback);
                }
                else
                {
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

                    if (callback != null && extractions != null)
                        callback(extractions);
                }
            }
        }

        public static async Task<bool> RunArkParse(string saveFilePath, string mapName, string? extractName, bool extractDinos, bool extractPlayerPawns, bool extractItems, bool extractStructures, bool extractPlayers, bool extractTribes, bool fastExtract, List<KeyValuePair<JsonExportProfile, bool>>? extractions, Action<List<KeyValuePair<JsonExportProfile, bool>>>? callback = null)
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
                return false;
            }
            AddJsonExportProfileToSettingsPageDropDown(jep);

            if (SettingsPage._debugLogging != null && SettingsPage._debugLogging.HasValue && SettingsPage._debugLogging.Value)
                AddPythonTestFile();
            AddArkParseRunner(jep);
            string arkParseRunnerPath = Utils.ArkParseRunnerFilePath();
            if (string.IsNullOrWhiteSpace(arkParseRunnerPath) || !File.Exists(arkParseRunnerPath))
            {
                HideInstallingPopup();
                return false;
            }

            //Task.Run(() => DoRunArkParse(arkParseRunnerPath, jep, extractions, callback));
            Task.Run(async () => await DoRunArkParse(arkParseRunnerPath, true, jep, extractions, callback));
            return true;
        }
#pragma warning restore CS1998, CS4014
    }
}
