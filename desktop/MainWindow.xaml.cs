using Microsoft.Web.WebView2.Core;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AstroDeviceHub.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += InitializeBrowserAsync;
    }

    private async void InitializeBrowserAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AstroDeviceHub", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null, dataDirectory);
            await Browser.EnsureCoreWebView2Async(environment);
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Browser.NavigationCompleted += (_, args) => { if (args.IsSuccess) LoadingPanel.Visibility = Visibility.Collapsed; };
            Browser.Source = new Uri("http://127.0.0.1:5000/?desktop=1");
        }
        catch (Exception ex)
        {
            LoadingPanel.Child = new TextBlock { Text = "WebView2 初始化失败\r\n" + ex.Message, Foreground = System.Windows.Media.Brushes.IndianRed, TextAlignment = TextAlignment.Center };
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ToggleMaximize(); else DragMove();
    }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
