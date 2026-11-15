using System.Windows;

namespace DownloadsInTray;

/// <summary>Changes apply and save immediately, so there is no Save button.</summary>
public partial class SettingsWindow : Window
{
    private readonly bool _loaded;

    public SettingsWindow()
    {
        InitializeComponent();
        Title = $"{AppInfo.Name} Settings";
        StartAtLogin.IsChecked = StartupRegistration.IsEnabled;
        MonochromeTrayIcon.IsChecked = AppSettings.Current.MonochromeTrayIcon;
        NotificationTime.Minimum = AppSettings.MinNotificationSeconds;
        NotificationTime.Maximum = AppSettings.MaxNotificationSeconds;
        NotificationTime.Value = AppSettings.Current.NotificationSeconds;
        VisibleItems.Minimum = AppSettings.MinVisibleItems;
        VisibleItems.Maximum = AppSettings.MaxVisibleItems;
        VisibleItems.Value = AppSettings.Current.VisibleItems;
        ShowNotificationTime();
        ShowVisibleItems();
        _loaded = true;
    }

    private void OnStartAtLoginClick(object sender, RoutedEventArgs e)
    {
        StartupRegistration.IsEnabled = StartAtLogin.IsChecked == true;
    }

    private void OnMonochromeTrayIconClick(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.MonochromeTrayIcon = MonochromeTrayIcon.IsChecked == true;
        AppSettings.Current.Save();
    }

    private void OnNotificationTimeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        AppSettings.Current.NotificationSeconds = (int)NotificationTime.Value;
        AppSettings.Current.Save();
        ShowNotificationTime();
    }

    private void OnVisibleItemsChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_loaded) return;
        AppSettings.Current.VisibleItems = (int)VisibleItems.Value;
        AppSettings.Current.Save();
        ShowVisibleItems();
    }

    private void ShowVisibleItems() => VisibleItemsValue.Text = $"{(int)VisibleItems.Value} items";

    private void ShowNotificationTime() => NotificationTimeValue.Text = $"{(int)NotificationTime.Value} seconds";
}
