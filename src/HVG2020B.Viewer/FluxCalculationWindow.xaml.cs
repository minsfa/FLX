using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using HVG2020B.Core;
using HVG2020B.Viewer.ViewModels;
using ScottPlot;

namespace HVG2020B.Viewer;

public partial class FluxCalculationWindow : Window
{
    private readonly FluxCalculationViewModel _viewModel;
    private ScottPlot.Plottables.Scatter? _dataPlot;
    private ScottPlot.Plottables.Scatter? _rangePlot;
    private ScottPlot.Plottables.VerticalLine? _startLine;
    private ScottPlot.Plottables.VerticalLine? _endLine;

    public FluxCalculationSnapshot? CalculationResult => _viewModel.LastCalculation;

    public FluxCalculationWindow(List<double> timeData, List<double> pressureData,
        string? csvFolderPath = null)
    {
        InitializeComponent();

        _viewModel = new FluxCalculationViewModel();
        _viewModel.DefaultExportDirectory = csvFolderPath;
        DataContext = _viewModel;

        SetupChart();

        _viewModel.DataUpdated += OnDataUpdated;
        _viewModel.RangeChanged += OnRangeChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Setup chart mouse click handler
        PressureChart.MouseLeftButtonDown += OnChartMouseLeftButtonDown;

        // Load data from main view
        _viewModel.LoadFromMainView(timeData, pressureData);
    }

    private void SetupChart()
    {
        PressureChart.Plot.Title("Pressure vs Time");
        PressureChart.Plot.XLabel("Time (s)");
        UpdateChartYLabel();

        // Style the plot
        PressureChart.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#FFFFFF");
        PressureChart.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#F8F8F8");
    }

    private void UpdateChartYLabel()
    {
        PressureChart.Plot.YLabel(
            _viewModel.IsPermeationMode ? "Pressure (Pa)" : "Pressure (Torr)");
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FluxCalculationViewModel.IsPermeationMode))
        {
            UpdateChartYLabel();
            // Re-plot data with correct unit display
            OnDataUpdated();
        }
    }

    private void OnDataUpdated()
    {
        PressureChart.Plot.Clear();
        _rangePlot = null;
        _startLine = null;
        _endLine = null;

        if (_viewModel.TimeData.Count == 0) return;

        // Chart always shows data in internal unit (Pa), but in LeakRate mode convert to Torr for display
        double[] displayPressure;
        if (_viewModel.IsLeakRateMode)
        {
            displayPressure = _viewModel.PressureData
                .Select(p => p * FluxCalculator.PaToTorr).ToArray();
        }
        else
        {
            displayPressure = _viewModel.PressureData.ToArray();
        }

        _dataPlot = PressureChart.Plot.Add.Scatter(
            _viewModel.TimeData.ToArray(), displayPressure);

        _dataPlot.Color = ScottPlot.Color.FromHex("#2196F3");
        _dataPlot.LineWidth = 2;
        _dataPlot.MarkerSize = 0;

        PressureChart.Plot.Axes.AutoScale();
        PressureChart.Refresh();
    }

    private void OnRangeChanged()
    {
        // Remove previous markers
        if (_rangePlot != null)
            PressureChart.Plot.Remove(_rangePlot);
        if (_startLine != null)
            PressureChart.Plot.Remove(_startLine);
        if (_endLine != null)
            PressureChart.Plot.Remove(_endLine);

        // Use display-unit pressures for range visualization
        double startP, endP;
        if (_viewModel.IsLeakRateMode)
        {
            startP = _viewModel.StartPressure * FluxCalculator.PaToTorr;
            endP = _viewModel.EndPressure * FluxCalculator.PaToTorr;
        }
        else
        {
            startP = _viewModel.StartPressure;
            endP = _viewModel.EndPressure;
        }

        var rangeX = new double[] { _viewModel.StartTime, _viewModel.EndTime };
        var rangeY = new double[] { startP, endP };

        _rangePlot = PressureChart.Plot.Add.Scatter(rangeX, rangeY);
        _rangePlot.Color = ScottPlot.Color.FromHex("#E53935");
        _rangePlot.LineWidth = 2;
        _rangePlot.MarkerSize = 12;
        _rangePlot.MarkerShape = ScottPlot.MarkerShape.FilledCircle;

        // Add vertical line at start (green)
        _startLine = PressureChart.Plot.Add.VerticalLine(_viewModel.StartTime);
        _startLine.Color = ScottPlot.Color.FromHex("#4CAF50");
        _startLine.LineWidth = 2;
        _startLine.LinePattern = LinePattern.Dashed;

        // Add vertical line at end (red)
        _endLine = PressureChart.Plot.Add.VerticalLine(_viewModel.EndTime);
        _endLine.Color = ScottPlot.Color.FromHex("#E53935");
        _endLine.LineWidth = 2;
        _endLine.LinePattern = LinePattern.Dashed;

        PressureChart.Refresh();
    }

    private void OnChartMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Check if Shift or Ctrl is pressed
        bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
        bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

        if (!isShiftPressed && !isCtrlPressed)
            return;

        // Convert mouse position to data coordinates with DPI scaling
        var position = e.GetPosition(PressureChart);
        var dpi = VisualTreeHelper.GetDpi(PressureChart);
        var pixel = new Pixel(
            (float)(position.X * dpi.DpiScaleX),
            (float)(position.Y * dpi.DpiScaleY));
        var coordinates = PressureChart.Plot.GetCoordinates(pixel);

        // Get the time value (X coordinate) and snap to 0.1s increments
        double clickedTime = Math.Round(coordinates.X * 10) / 10.0;

        // Clamp to valid range
        clickedTime = Math.Max(_viewModel.MinTime, Math.Min(_viewModel.MaxTime, clickedTime));

        if (isShiftPressed)
        {
            // Set Start Time
            _viewModel.StartTime = clickedTime;
        }
        else if (isCtrlPressed)
        {
            // Set End Time
            _viewModel.EndTime = clickedTime;
        }

        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        PressureChart.MouseLeftButtonDown -= OnChartMouseLeftButtonDown;
        _viewModel.DataUpdated -= OnDataUpdated;
        _viewModel.RangeChanged -= OnRangeChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnClosed(e);
    }
}
