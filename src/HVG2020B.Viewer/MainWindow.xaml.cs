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

        PressureChart.Refresh();
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

        // Auto-scale
        PressureChart.Plot.Axes.AutoScale();

        PressureChart.Refresh();
    }
}
