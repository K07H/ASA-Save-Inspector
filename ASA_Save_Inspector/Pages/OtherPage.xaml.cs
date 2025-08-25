using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace ASA_Save_Inspector.Pages
{
    public sealed partial class OtherPage : Page
    {
        public OtherPage()
        {
            InitializeComponent();
        }

        private void btn_OpenAppDataFolder_Click(object sender, RoutedEventArgs e)
        {
            bool dirExists = true;
            string folderPath = Utils.GetDataDir();
            Logger.Instance.Log($"AppDataFolderPath=[{folderPath}]");
            if (!Directory.Exists(folderPath))
            {
                folderPath = Utils.GetBaseDir();
                if (!Directory.Exists(folderPath))
                    dirExists = false;
            }
            if (dirExists)
                Process.Start(Environment.GetEnvironmentVariable("WINDIR") + @"\explorer.exe", $"\"{folderPath}\"");
            else
                MainWindow.ShowToast("Error: Unable to locate ASI folder.", BackgroundColor.ERROR);
        }

        private void btn_OpenMinimap_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.OpenMinimap();
#if DEBUG
            MainWindow.TestAddPointsMinimap();
#endif
        }

        private void btn_ForceArkParseUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(Utils.ArkParseFolder()))
            {
                try
                {
                    Directory.Delete(Utils.ArkParseFolder(), true);
                    if (MainWindow._mainWindow != null)
                        MainWindow._mainWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                        {
                            if (MainWindow._mainWindow != null)
                            {
                                if (MainWindow._mainWindow._navView != null)
                                    MainWindow._mainWindow._navView.SelectedItem = MainWindow._mainWindow._navBtnSettings;
                                MainWindow._mainWindow.NavView_Navigate(typeof(SettingsPage), new EntranceNavigationTransitionInfo());
                            }
                        });
                }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Failed to delete ArkParse folder. Exception=[{ex}]", Logger.LogLevel.ERROR);
                    MainWindow.ShowToast("Failed to reinstall ArkParse, please check logs.");
                }
            }
        }
    }
}
