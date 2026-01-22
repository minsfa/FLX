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

    private void OnDataUpdated()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(OnDataUpdated);
            return;
        }

        var timeData = _viewModel.TimeData;
        var pressureData = _viewModel.PressureData;

        if (timeData.Count == 0)
        {
            PressureChart.Plot.Clear();
            PressureChart.Refresh();
            return;
        }

        // Use Scatter plot for real-time data
        PressureChart.Plot.Clear();

        var scatter = PressureChart.Plot.Add.Scatter(
            timeData.ToArray(),
            pressureData.ToArray());

        scatter.Color = ScottPlot.Color.FromHex("#2196F3");
        scatter.LineWidth = 2;
        scatter.MarkerSize = 0;

        if (_viewModel.UseLogScale)
        {
            // For log scale, transform data and use custom tick labels
            var logPressureData = pressureData.Select(p => Math.Log10(Math.Max(p, 1e-12))).ToArray();

            PressureChart.Plot.Clear();
            var logScatter = PressureChart.Plot.Add.Scatter(timeData.ToArray(), logPressureData);
            logScatter.Color = ScottPlot.Color.FromHex("#2196F3");
            logScatter.LineWidth = 2;
            logScatter.MarkerSize = 0;

            // Set Y limits based on log-transformed data
            var yMin = logPressureData.Min() - 1;
            var yMax = logPressureData.Max() + 1;
            PressureChart.Plot.Axes.SetLimitsY(yMin, yMax);

            // Custom tick generator with log-scale labels
            var tickGen = new ScottPlot.TickGenerators.NumericAutomatic();
            tickGen.LabelFormatter = LogTickLabelFormatter;
            PressureChart.Plot.Axes.Left.TickGenerator = tickGen;
            PressureChart.Plot.Axes.Left.Label.Text = "Pressure (Torr) [LOG]";
        }
        else
        {
            // Linear scale with auto-scale
            PressureChart.Plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic();
            PressureChart.Plot.Axes.Left.Label.Text = "Pressure (Torr)";
            PressureChart.Plot.Axes.AutoScale();
        }

        // Always auto-scale X axis
        PressureChart.Plot.Axes.AutoScaleX();

        PressureChart.Refresh();
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
