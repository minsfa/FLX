using CommunityToolkit.Mvvm.ComponentModel;
using HVG2020B.Core;

namespace HVG2020B.Viewer.ViewModels;

public partial class ScannedDevice : ObservableObject
{
    public string DeviceId { get; init; } = "";

    public string PortName { get; init; } = "";

    public string DeviceType { get; init; } = "HVG-2020B";

    public IGaugeDevice Device { get; init; } = null!;

    [ObservableProperty]
    private bool _isAdded;
}
