using Microsoft.UI.Xaml.Controls;

namespace ASA_Save_Inspector.Pages
{
    public sealed partial class AboutPage : Page
    {
        public AboutPage()
        {
            InitializeComponent();
            tb_AppName.Text = $"Name: {MainWindow._appName} ({MainWindow._appAcronym})";
            tb_Version.Text = $"Version: {Utils.GetVersionStr()}";
        }
    }
}
