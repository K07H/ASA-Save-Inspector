using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace ASA_Save_Inspector.Pages
{
    public sealed partial class OtherPage : Page, INotifyPropertyChanged
    {
        public OtherPage()
        {
            InitializeComponent();

            // Calculate page center.
            AdjustToSizeChange();

            SettingsPage.LoadCustomBlueprints();
            RefreshRegisteredBlueprints();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private int _windowWidth = 0;
        public int WindowWidth
        {
            get { return _windowWidth; }
            set { _windowWidth = value; OnPropertyChanged(); }
        }
        private int _windowHeight = 0;
        public int WindowHeight
        {
            get { return _windowHeight; }
            set { _windowHeight = value; OnPropertyChanged(); }
        }

#pragma warning disable CS8625
        public void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
#pragma warning restore CS8625

        private void AdjustToSizeChange()
        {
            if (MainWindow._mainWindow != null)
            {
                NavigationViewDisplayMode displayMode = NavigationViewDisplayMode.Expanded;
                if (MainWindow._mainWindow._navView != null)
                    displayMode = MainWindow._mainWindow._navView.DisplayMode;
                WindowWidth = Math.Max(1, Convert.ToInt32(Math.Round(MainWindow._mainWindow.Bounds.Width)) - (displayMode == NavigationViewDisplayMode.Minimal ? 2 : (displayMode == NavigationViewDisplayMode.Compact ? 52 : 164)));
                WindowHeight = Math.Max(1, Convert.ToInt32(Math.Round(MainWindow._mainWindow.Bounds.Height)) - 52);
            }
        }

        private void page_SizeChanged(object sender, SizeChangedEventArgs e) => AdjustToSizeChange();

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

#pragma warning disable CS1998
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
#pragma warning restore CS1998

        private void ResetCustomBlueprintPopup()
        {
            tb_BlueprintType.Text = "Click here...";
            tb_BlueprintClass.Text = "";
            sp_BlueprintClass.Visibility = Visibility.Collapsed;
            tb_RegisteredBlueprintsType.Text = "Click here...";
            sp_RegisteredDinoBlueprintsBlock.Visibility = Visibility.Collapsed;
            sp_RegisteredItemBlueprintsBlock.Visibility = Visibility.Collapsed;
            sp_RegisteredStructureBlueprintsBlock.Visibility = Visibility.Collapsed;
        }

        private void BlueprintTypeSelected(string? blueprintType)
        {
            if (!string.IsNullOrEmpty(blueprintType))
            {
                tb_BlueprintType.Text = blueprintType;
                sp_BlueprintClass.Visibility = Visibility.Visible;
            }
        }

        private string? GetSelectedMenuFlyoutItem(object sender)
        {
            MenuFlyoutItem? mfi = (sender as MenuFlyoutItem);
            if (mfi != null)
                return mfi.Text;
            return null;
        }

        private Grid? GetUIElemForBlueprint(string blueprint, RoutedEventHandler clickEventFunc)
        {
            if (string.IsNullOrWhiteSpace(blueprint))
                return null;

            Grid grd = new Grid()
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0.0d, 5.0d, 0.0d, 0.0d)
            };
            grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
            grd.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
            TextBox tb = new TextBox()
            {
                FontSize = 12.0d,
                TextWrapping = TextWrapping.NoWrap,
                AcceptsReturn = false,
                Text = blueprint,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 500.0d,
                IsReadOnly = true
            };
            Button btn = new Button()
            {
                Width = 90.0d,
                Content = "Remove",
                FontSize = 12.0d,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0.0d, 0.0d, 5.0d, 0.0d)
            };
            btn.Click += clickEventFunc;
            grd.Children.Add(btn);
            Grid.SetColumn(btn, 0);
            grd.Children.Add(tb);
            Grid.SetColumn(tb, 1);

            return grd;
        }

        private void RefreshRegisteredBlueprints()
        {
            sp_RegisteredDinoBlueprints.Children.Clear();
            sp_RegisteredItemBlueprints.Children.Clear();
            sp_RegisteredStructureBlueprints.Children.Clear();

            if (SettingsPage._customBlueprints?.Dinos != null && SettingsPage._customBlueprints.Dinos.Count > 0)
                foreach (string dinoBP in SettingsPage._customBlueprints.Dinos)
                    if (!string.IsNullOrWhiteSpace(dinoBP))
                    {
                        Grid? grd = GetUIElemForBlueprint(dinoBP, btn_RemoveCustomDinoBlueprint_Click);
                        if (grd != null)
                            sp_RegisteredDinoBlueprints.Children.Add(grd);
                    }

            if (SettingsPage._customBlueprints?.Items != null && SettingsPage._customBlueprints.Items.Count > 0)
                foreach (string itemBP in SettingsPage._customBlueprints.Items)
                    if (!string.IsNullOrWhiteSpace(itemBP))
                    {
                        Grid? grd = GetUIElemForBlueprint(itemBP, btn_RemoveCustomItemBlueprint_Click);
                        if (grd != null)
                            sp_RegisteredItemBlueprints.Children.Add(grd);
                    }

            if (SettingsPage._customBlueprints?.Structures != null && SettingsPage._customBlueprints.Structures.Count > 0)
                foreach (string structureBP in SettingsPage._customBlueprints.Structures)
                    if (!string.IsNullOrWhiteSpace(structureBP))
                    {
                        Grid? grd = GetUIElemForBlueprint(structureBP, btn_RemoveCustomStructureBlueprint_Click);
                        if (grd != null)
                            sp_RegisteredStructureBlueprints.Children.Add(grd);
                    }
        }

        private void btn_RemoveCustomDinoBlueprint_Click(object sender, RoutedEventArgs e)
        {
            Button? btn = sender as Button;
            if (btn == null)
                return;
            Grid? grd = btn.Parent as Grid;
            if (grd == null)
                return;
            TextBox? tb = grd.Children[1] as TextBox;
            if (tb == null)
                return;
            if (SettingsPage._customBlueprints?.Dinos == null || SettingsPage._customBlueprints.Dinos.Count <= 0)
                return;
            string? blueprint = tb.Text;
            if (string.IsNullOrWhiteSpace(blueprint))
                return;

            int toDel = -1;
            for (int i = 0; i < SettingsPage._customBlueprints.Dinos.Count; i++)
                if (string.Compare(blueprint, SettingsPage._customBlueprints.Dinos[i], StringComparison.InvariantCulture) == 0)
                {
                    toDel = i;
                    break;
                }

            if (toDel >= 0)
            {
                SettingsPage._customBlueprints.Dinos.RemoveAt(toDel);
                SettingsPage.SaveCustomBlueprints();
                RefreshRegisteredBlueprints();
                MainWindow.ShowToast("Custom dino blueprint unregistered.", BackgroundColor.SUCCESS);
            }
        }

        private void btn_RemoveCustomItemBlueprint_Click(object sender, RoutedEventArgs e)
        {
            Button? btn = sender as Button;
            if (btn == null)
                return;
            Grid? grd = btn.Parent as Grid;
            if (grd == null)
                return;
            TextBox? tb = grd.Children[1] as TextBox;
            if (tb == null)
                return;
            if (SettingsPage._customBlueprints?.Items == null || SettingsPage._customBlueprints.Items.Count <= 0)
                return;
            string? blueprint = tb.Text;
            if (string.IsNullOrWhiteSpace(blueprint))
                return;

            int toDel = -1;
            for (int i = 0; i < SettingsPage._customBlueprints.Items.Count; i++)
                if (string.Compare(blueprint, SettingsPage._customBlueprints.Items[i], StringComparison.InvariantCulture) == 0)
                {
                    toDel = i;
                    break;
                }

            if (toDel >= 0)
            {
                SettingsPage._customBlueprints.Items.RemoveAt(toDel);
                SettingsPage.SaveCustomBlueprints();
                RefreshRegisteredBlueprints();
                MainWindow.ShowToast("Custom item blueprint unregistered.", BackgroundColor.SUCCESS);
            }
        }

        private void btn_RemoveCustomStructureBlueprint_Click(object sender, RoutedEventArgs e)
        {
            Button? btn = sender as Button;
            if (btn == null)
                return;
            Grid? grd = btn.Parent as Grid;
            if (grd == null)
                return;
            TextBox? tb = grd.Children[1] as TextBox;
            if (tb == null)
                return;
            if (SettingsPage._customBlueprints?.Structures == null || SettingsPage._customBlueprints.Structures.Count <= 0)
                return;
            string? blueprint = tb.Text;
            if (string.IsNullOrWhiteSpace(blueprint))
                return;

            int toDel = -1;
            for (int i = 0; i < SettingsPage._customBlueprints.Structures.Count; i++)
                if (string.Compare(blueprint, SettingsPage._customBlueprints.Structures[i], StringComparison.InvariantCulture) == 0)
                {
                    toDel = i;
                    break;
                }

            if (toDel >= 0)
            {
                SettingsPage._customBlueprints.Structures.RemoveAt(toDel);
                SettingsPage.SaveCustomBlueprints();
                RefreshRegisteredBlueprints();
                MainWindow.ShowToast("Custom structure blueprint unregistered.", BackgroundColor.SUCCESS);
            }
        }

        private void btn_ConfigureCustomBlueprints_Click(object sender, RoutedEventArgs e)
        {
            if (AddCustomBlueprintsPopup.IsOpen)
                return;

            ResetCustomBlueprintPopup();
            AddCustomBlueprintsPopup.IsOpen = true;
        }

        private void mfi_BlueprintTypeDino_Click(object sender, RoutedEventArgs e) => BlueprintTypeSelected(GetSelectedMenuFlyoutItem(sender));

        private void mfi_BlueprintTypeItem_Click(object sender, RoutedEventArgs e) => BlueprintTypeSelected(GetSelectedMenuFlyoutItem(sender));

        private void mfi_BlueprintTypeStructure_Click(object sender, RoutedEventArgs e) => BlueprintTypeSelected(GetSelectedMenuFlyoutItem(sender));

        private void btn_CloseAddCustomBlueprintsPopup_Click(object sender, RoutedEventArgs e)
        {
            if (AddCustomBlueprintsPopup.IsOpen)
            {
                ResetCustomBlueprintPopup();
                AddCustomBlueprintsPopup.IsOpen = false;
            }
        }

        private void btn_AddCustomBlueprint_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tb_BlueprintClass.Text))
            {
                MainWindow.ShowToast("Cannot add blueprint without class name.", BackgroundColor.WARNING);
                return;
            }

            bool isDinoBP = (string.Compare(tb_BlueprintType.Text, "Dino blueprint", StringComparison.InvariantCulture) == 0);
            bool isItemBP = (string.Compare(tb_BlueprintType.Text, "Item blueprint", StringComparison.InvariantCulture) == 0);
            bool isStructureBP = (string.Compare(tb_BlueprintType.Text, "Structure blueprint", StringComparison.InvariantCulture) == 0);
            if (!isDinoBP && !isItemBP && !isStructureBP)
            {
                MainWindow.ShowToast("Bad blueprint type selected.", BackgroundColor.WARNING);
                return;
            }

            if (isDinoBP)
            {
                if (SettingsPage._customBlueprints?.Dinos != null)
                {
                    if (SettingsPage._customBlueprints.Dinos.Count > 0)
                        foreach (string str in SettingsPage._customBlueprints.Dinos)
                            if (string.Compare(str, tb_BlueprintClass.Text, StringComparison.InvariantCulture) == 0)
                            {
                                MainWindow.ShowToast("Custom dino blueprint already registered.", BackgroundColor.WARNING);
                                return;
                            }
                    SettingsPage._customBlueprints.Dinos.Add(tb_BlueprintClass.Text);
                    MainWindow.ShowToast("Custom dino blueprint has been added.", BackgroundColor.SUCCESS);
                }
            }
            else if (isItemBP)
            {
                if (SettingsPage._customBlueprints?.Items != null)
                {
                    if (SettingsPage._customBlueprints.Items.Count > 0)
                        foreach (string str in SettingsPage._customBlueprints.Items)
                            if (string.Compare(str, tb_BlueprintClass.Text, StringComparison.InvariantCulture) == 0)
                            {
                                MainWindow.ShowToast("Custom item blueprint already registered.", BackgroundColor.WARNING);
                                return;
                            }
                    SettingsPage._customBlueprints.Items.Add(tb_BlueprintClass.Text);
                    MainWindow.ShowToast("Custom item blueprint has been added.", BackgroundColor.SUCCESS);
                }
            }
            else if (isStructureBP)
            {
                if (SettingsPage._customBlueprints?.Structures != null)
                {
                    if (SettingsPage._customBlueprints.Structures.Count > 0)
                        foreach (string str in SettingsPage._customBlueprints.Structures)
                            if (string.Compare(str, tb_BlueprintClass.Text, StringComparison.InvariantCulture) == 0)
                            {
                                MainWindow.ShowToast("Custom structure blueprint already registered.", BackgroundColor.WARNING);
                                return;
                            }
                    SettingsPage._customBlueprints.Structures.Add(tb_BlueprintClass.Text);
                    MainWindow.ShowToast("Custom structure blueprint has been added.", BackgroundColor.SUCCESS);
                }
            }
            SettingsPage.SaveCustomBlueprints();
            RefreshRegisteredBlueprints();
        }

        private void tb_BlueprintClass_TextChanged(object sender, TextChangedEventArgs e)
        {
            btn_AddCustomBlueprint.IsEnabled = (!string.IsNullOrWhiteSpace(tb_BlueprintClass.Text) && tb_BlueprintClass.Text.Length > 0);
        }

        private void RegisteredBlueprintsTypeSelected(string? blueprintType)
        {
            sp_RegisteredDinoBlueprintsBlock.Visibility = Visibility.Collapsed;
            sp_RegisteredItemBlueprintsBlock.Visibility = Visibility.Collapsed;
            sp_RegisteredStructureBlueprintsBlock.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(blueprintType))
                return;

            tb_RegisteredBlueprintsType.Text = blueprintType;

            if (string.Compare(blueprintType, "Dino blueprints", StringComparison.InvariantCulture) == 0)
                sp_RegisteredDinoBlueprintsBlock.Visibility = Visibility.Visible;
            else if (string.Compare(blueprintType, "Item blueprints", StringComparison.InvariantCulture) == 0)
                sp_RegisteredItemBlueprintsBlock.Visibility = Visibility.Visible;
            else if (string.Compare(blueprintType, "Structure blueprints", StringComparison.InvariantCulture) == 0)
                sp_RegisteredStructureBlueprintsBlock.Visibility = Visibility.Visible;
        }

        private void mfi_RegisteredBlueprintTypeDinos_Click(object sender, RoutedEventArgs e) => RegisteredBlueprintsTypeSelected(GetSelectedMenuFlyoutItem(sender));

        private void mfi_RegisteredBlueprintTypeItems_Click(object sender, RoutedEventArgs e) => RegisteredBlueprintsTypeSelected(GetSelectedMenuFlyoutItem(sender));

        private void mfi_RegisteredBlueprintTypeStructures_Click(object sender, RoutedEventArgs e) => RegisteredBlueprintsTypeSelected(GetSelectedMenuFlyoutItem(sender));
    }
}
