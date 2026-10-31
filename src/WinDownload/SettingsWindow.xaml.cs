using System.Windows;

namespace WinDownload;

/// <summary>Changes apply and save immediately, so there is no Save button.</summary>
public partial class SettingsWindow : Window
{
    private readonly bool _loaded;

    public SettingsWindow()
    {
        InitializeComponent();
        StartAtLogin.IsChecked = StartupRegistration.IsEnabled;
        NotificationTime.Minimum = AppSettings.MinNotificationSeconds;
        NotificationTime.Maximum = AppSettings.MaxNotificationSeconds;
        NotificationTime.Value = AppSettings.Current.NotificationSeconds;
        ShowNotificationTime();
        _loaded = true;
    }

    private void OnStartAtLoginClick(object sender, RoutedEventArgs e)
    {
        StartupRegistration.IsEnabled = StartAtLogin.IsChecked == true;
    }

    private void OnNotificationTimeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        AppSettings.Current.NotificationSeconds = (int)NotificationTime.Value;
        AppSettings.Current.Save();
        ShowNotificationTime();
    }

    private void ShowNotificationTime() => NotificationTimeValue.Text = $"{(int)NotificationTime.Value} seconds";
}
