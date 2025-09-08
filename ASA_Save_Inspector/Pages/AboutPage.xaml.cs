using Microsoft.UI.Xaml.Controls;

namespace ASA_Save_Inspector.Pages
{
    public sealed partial class AboutPage : Page
    {
        public static string AppName => MainWindow._appName;
        public static string AppAcronym => MainWindow._appAcronym;
        public static string AppVersion => Utils.GetVersionStr();

        public static AboutPage? _page = null;

        public AboutPage()
        {
            InitializeComponent();
            _page = this;
            tb_AppName.Text = $"{ASILang.Get("Name")}: {MainWindow._appName} ({MainWindow._appAcronym})";
            tb_Version.Text = $"{ASILang.Get("Version")}: {Utils.GetVersionStr()}";
        }
    }
}
