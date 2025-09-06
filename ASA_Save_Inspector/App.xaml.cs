using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;

namespace ASA_Save_Inspector
{
    public partial class App : Application
    {
        public static Window? _window = null;
        //public static App? _app = null;

        public App()
        {
            InitializeComponent();
            Pages.SettingsPage.LoadSettingsStatic();
            RequestedTheme = (Pages.SettingsPage._darkTheme != null && Pages.SettingsPage._darkTheme.HasValue && !Pages.SettingsPage._darkTheme.Value ? ApplicationTheme.Light : ApplicationTheme.Dark);
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
