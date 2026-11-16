using Microsoft.UI.Xaml;
using System;

namespace ASA_Save_Inspector
{
    public partial class App : Application
    {
        public static Window? _window = null;

        public App()
        {
            bool isCliLaunch = SingleInstance.IsCliLaunch();
            if (!SingleInstance.TryAcquire(isCliLaunch ? SingleInstance.CLI_WAIT_MILLISECONDS : SingleInstance.GUI_WAIT_MILLISECONDS))
            {
                if (!isCliLaunch)
                    SingleInstance.ShowAlreadyRunningMessage();
                Environment.Exit(SingleInstance.EXIT_ALREADY_RUNNING);
                return;
            }

            InitializeComponent();
            Pages.SettingsPage.LoadSettingsStatic(true);
            RequestedTheme = (Pages.SettingsPage._darkTheme != null && Pages.SettingsPage._darkTheme.HasValue && !Pages.SettingsPage._darkTheme.Value ? ApplicationTheme.Light : ApplicationTheme.Dark);
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
