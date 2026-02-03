using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using HVG2020B.Viewer.ViewModels;
using ScottPlot;

namespace HVG2020B.Viewer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = (MainViewModel)DataContext;
        _viewModel.DataUpdated += OnDataUpdated;
        _viewModel.ScaleChanged += OnScaleChanged;
        _viewModel.DeviceSeries.CollectionChanged += OnDeviceSeriesChanged;

        SetupChart();

        Closing += (_, _) => _viewModel.Dispose();
    }

    private void SetupChart()
    {
        var plot = PressureChart.Plot;

        // Dark theme colors
        plot.FigureBackground.Color = ScottPlot.Color.FromHex("#2D2D2D");
        plot.DataBackground.Color = ScottPlot.Color.FromHex("#1E1E1E");

        // Configure axes labels
        plot.Axes.Bottom.Label.Text = "Time (seconds)";
        plot.Axes.Left.Label.Text = "Pressure (Torr)";

        // Grid
        plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#333333");

        // Apply initial scale setting
        ApplyScale();

        PressureChart.Refresh();
    }

    private void ApplyScale()
    {
        var plot = PressureChart.Plot;

        if (_viewModel.UseLogScale)
        {
            // Use logarithmic scale for Y-axis
            plot.Axes.Left.Min = 1e-12;
            plot.Axes.SetLimitsY(1e-12, 1e4);
        }
        else
        {
            // Use linear scale (auto-scale will handle limits)
            plot.Axes.Left.Min = 0;
        }
    }

    private void OnScaleChanged()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(OnScaleChanged);
            return;
        }

        ApplyScale();
        OnDataUpdated(); // Re-render with new scale
    }

    private void OnDeviceSeriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnDataUpdated();
    }

    private void OnDataUpdated()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(OnDataUpdated);
            return;
        }

        var seriesList = _viewModel.DeviceSeries;
        var visibleIds = new HashSet<string>(
            _viewModel.Devices.Where(d => d.IsVisibleOnChart).Select(d => d.DeviceId));
        PressureChart.Plot.Clear();

        if (_viewModel.UseLogScale)
        {
            double? yMin = null;
            double? yMax = null;

            foreach (var series in seriesList)
            {
                if (series.TimeData.Count == 0 || !visibleIds.Contains(series.DeviceId))
                {
                    continue;
                }

                var logPressureData = series.PressureData
                    .Select(p => Math.Log10(Math.Max(p, 1e-12)))
                    .ToArray();

                var logScatter = PressureChart.Plot.Add.Scatter(
                    series.TimeData.ToArray(),
                    logPressureData);
                logScatter.Color = ScottPlot.Color.FromHex(series.ColorHex);
                logScatter.LineWidth = 2;
                logScatter.MarkerSize = 0;
                logScatter.LegendText = series.DeviceId;

                var localMin = logPressureData.Min();
                var localMax = logPressureData.Max();
                yMin = yMin.HasValue ? Math.Min(yMin.Value, localMin) : localMin;
                yMax = yMax.HasValue ? Math.Max(yMax.Value, localMax) : localMax;
            }

            if (yMin.HasValue && yMax.HasValue)
            {
                PressureChart.Plot.Axes.SetLimitsY(yMin.Value - 1, yMax.Value + 1);
            }

            // Custom tick generator with log-scale labels
            var tickGen = new ScottPlot.TickGenerators.NumericAutomatic();
            tickGen.LabelFormatter = LogTickLabelFormatter;
            PressureChart.Plot.Axes.Left.TickGenerator = tickGen;
            PressureChart.Plot.Axes.Left.Label.Text = "Pressure (Torr) [LOG]";
        }
        else
        {
            foreach (var series in seriesList)
            {
                if (series.TimeData.Count == 0 || !visibleIds.Contains(series.DeviceId))
                {
                    continue;
                }

                var scatter = PressureChart.Plot.Add.Scatter(
                    series.TimeData.ToArray(),
                    series.PressureData.ToArray());
                scatter.Color = ScottPlot.Color.FromHex(series.ColorHex);
                scatter.LineWidth = 2;
                scatter.MarkerSize = 0;
                scatter.LegendText = series.DeviceId;
            }

            PressureChart.Plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic();
            PressureChart.Plot.Axes.Left.Label.Text = "Pressure (Torr)";
            PressureChart.Plot.Axes.AutoScale();
        }

        PressureChart.Plot.Legend.IsVisible = seriesList.Count > 0;
        PressureChart.Plot.Legend.Alignment = Alignment.UpperRight;

        // Always auto-scale X axis
        PressureChart.Plot.Axes.AutoScaleX();

        PressureChart.Refresh();
    }

    private async void OnConnectClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DeviceItem deviceItem)
        {
            await _viewModel.ConnectDeviceCommand.ExecuteAsync(deviceItem);
        }
    }

    private void OnDisconnectClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is DeviceItem deviceItem)
        {
            _viewModel.DisconnectDeviceCommand.Execute(deviceItem);
        }
    }

    /// <summary>
    /// Format log-scale tick labels as scientific notation (10^x)
    /// </summary>
    private static string LogTickLabelFormatter(double logValue)
    {
        var actualValue = Math.Pow(10, logValue);
        return actualValue.ToString("E1");
    }
}
