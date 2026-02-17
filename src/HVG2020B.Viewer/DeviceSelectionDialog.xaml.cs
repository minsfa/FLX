using System.Windows;

namespace HVG2020B.Viewer;

public partial class DeviceSelectionDialog : Window
{
    public string? SelectedDeviceId { get; private set; }

    public DeviceSelectionDialog(List<string> deviceIds)
    {
        InitializeComponent();
        DeviceListBox.ItemsSource = deviceIds;
        if (deviceIds.Count > 0)
            DeviceListBox.SelectedIndex = 0;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        SelectedDeviceId = DeviceListBox.SelectedItem as string;
        DialogResult = SelectedDeviceId != null;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
