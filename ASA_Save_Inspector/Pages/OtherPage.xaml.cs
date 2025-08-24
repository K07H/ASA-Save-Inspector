using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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
    }
}
