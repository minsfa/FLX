using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HVG2020B.Core;
using Microsoft.Win32;

namespace HVG2020B.Viewer.ViewModels;

public partial class FluxCalculationViewModel : ObservableObject
{
    private readonly FluxCalculator _calculator;
    private readonly FluxCalculator.Parameters _parameters = new();

    public FluxCalculationViewModel()
    {
        _calculator = new FluxCalculator();
    }

    #region Input Parameters

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Flux))]
    [NotifyPropertyChangedFor(nameof(Permeance))]
    [NotifyPropertyChangedFor(nameof(PermeanceGpu))]
    private double _membraneArea = 1e-4; // 1 cm² default

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Flux))]
    [NotifyPropertyChangedFor(nameof(Permeance))]
    [NotifyPropertyChangedFor(nameof(PermeanceGpu))]
    private double _temperature = 298.15; // 25°C

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PressureDrop))]
    [NotifyPropertyChangedFor(nameof(Permeance))]
    [NotifyPropertyChangedFor(nameof(PermeanceGpu))]
    private double _feedSidePressure = 101325; // 1 atm

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Flux))]
    [NotifyPropertyChangedFor(nameof(Permeance))]
    [NotifyPropertyChangedFor(nameof(PermeanceGpu))]
    private double _chamberVolume = 1e-6; // 1 cm³

    #endregion

    #region Time Range Selection

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Period))]
    [NotifyPropertyChangedFor(nameof(StartPressure))]
    [NotifyCanExecuteChangedFor(nameof(CalculateCommand))]
    private double _startTime;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Period))]
    [NotifyPropertyChangedFor(nameof(EndPressure))]
    [NotifyCanExecuteChangedFor(nameof(CalculateCommand))]
    private double _endTime;

    [ObservableProperty]
    private double _minTime;

    [ObservableProperty]
    private double _maxTime;

    #endregion

    #region Calculated Results

    [ObservableProperty]
    private double _startPressure;

    [ObservableProperty]
    private double _endPressure;

    public double Period => Math.Abs(EndTime - StartTime);

    [ObservableProperty]
    private double _pressureChangeRate;

    [ObservableProperty]
    private double? _linearizationSlope;

    [ObservableProperty]
    private double _pressureDrop;

    [ObservableProperty]
    private double _flux;

    [ObservableProperty]
    private double _permeance;

    [ObservableProperty]
    private double _permeanceGpu;

    [ObservableProperty]
    private double? _rSquared;

    [ObservableProperty]
    private int _dataPointCount;

    [ObservableProperty]
    private string _statusMessage = "Load data to begin";

    [ObservableProperty]
    private bool _hasResult;

    #endregion

    #region Chart Data

    public List<double> TimeData { get; } = new();
    public List<double> PressureData { get; } = new();

    public event Action? DataUpdated;
    public event Action? RangeChanged;

    #endregion

    /// <summary>
    /// Loads pressure data from arrays.
    /// </summary>
    public void LoadData(double[] timeSeconds, double[] pressureTorr)
    {
        _calculator.Clear();
        TimeData.Clear();
        PressureData.Clear();

        // Convert Torr to Pa (1 Torr = 133.322 Pa)
        const double torrToPa = 133.322;

        for (int i = 0; i < timeSeconds.Length; i++)
        {
            double pressurePa = pressureTorr[i] * torrToPa;
            _calculator.AddDataPoint(timeSeconds[i], pressurePa);
            TimeData.Add(timeSeconds[i]);
            PressureData.Add(pressurePa);
        }

        if (timeSeconds.Length > 0)
        {
            MinTime = timeSeconds.Min();
            MaxTime = timeSeconds.Max();
            StartTime = MinTime;
            EndTime = MaxTime;
            DataPointCount = timeSeconds.Length;
            StatusMessage = $"Loaded {timeSeconds.Length} data points";
        }

        DataUpdated?.Invoke();
        CalculateCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Loads data from the main view's chart data.
    /// </summary>
    public void LoadFromMainView(List<double> timeData, List<double> pressureData)
    {
        if (timeData.Count == 0 || pressureData.Count == 0)
        {
            StatusMessage = "No data available";
            return;
        }

        LoadData(timeData.ToArray(), pressureData.ToArray());
    }

    [RelayCommand(CanExecute = nameof(CanCalculate))]
    private void Calculate()
    {
        try
        {
            _parameters.MembraneArea = MembraneArea;
            _parameters.Temperature = Temperature;
            _parameters.FeedSidePressure = FeedSidePressure;
            _parameters.ChamberVolume = ChamberVolume;

            var result = _calculator.Calculate(_parameters, StartTime, EndTime);

            StartPressure = result.StartPressure;
            EndPressure = result.EndPressure;
            PressureChangeRate = result.PressureChangeRate;
            LinearizationSlope = result.LinearizationSlope;
            PressureDrop = result.PressureDrop;
            Flux = result.Flux;
            Permeance = result.Permeance;
            PermeanceGpu = result.PermeanceGpu;
            RSquared = result.RSquared;
            DataPointCount = result.DataPointCount;
            HasResult = true;

            StatusMessage = $"Calculated ({result.DataPointCount} points, R²={result.RSquared:F4})";
            RangeChanged?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            HasResult = false;
        }
    }

    private bool CanCalculate() => _calculator.Data.Count >= 2 && Period > 0;

    [RelayCommand]
    private void ExportToCsv()
    {
        if (!HasResult) return;

        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = $"flux_calculation_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                ExportResults(dialog.FileName);
                StatusMessage = $"Exported to {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
        }
    }

    private void ExportResults(string filePath)
    {
        using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);

        writer.WriteLine("Parameter,Value,Unit");
        writer.WriteLine($"Membrane Area,{MembraneArea.ToString(CultureInfo.InvariantCulture)},m²");
        writer.WriteLine($"Temperature,{Temperature.ToString(CultureInfo.InvariantCulture)},K");
        writer.WriteLine($"Feed Side Pressure,{FeedSidePressure.ToString(CultureInfo.InvariantCulture)},Pa");
        writer.WriteLine($"Chamber Volume,{ChamberVolume.ToString(CultureInfo.InvariantCulture)},m³");
        writer.WriteLine();
        writer.WriteLine($"Start Time,{StartTime.ToString(CultureInfo.InvariantCulture)},s");
        writer.WriteLine($"End Time,{EndTime.ToString(CultureInfo.InvariantCulture)},s");
        writer.WriteLine($"Period,{Period.ToString(CultureInfo.InvariantCulture)},s");
        writer.WriteLine($"Start Pressure,{StartPressure.ToString(CultureInfo.InvariantCulture)},Pa");
        writer.WriteLine($"End Pressure,{EndPressure.ToString(CultureInfo.InvariantCulture)},Pa");
        writer.WriteLine($"Pressure Change Rate,{PressureChangeRate.ToString(CultureInfo.InvariantCulture)},Pa/s");
        writer.WriteLine($"Linearization Slope,{LinearizationSlope?.ToString(CultureInfo.InvariantCulture) ?? "N/A"},Pa/s");
        writer.WriteLine($"Pressure Drop,{PressureDrop.ToString(CultureInfo.InvariantCulture)},Pa");
        writer.WriteLine($"R-Squared,{RSquared?.ToString(CultureInfo.InvariantCulture) ?? "N/A"},");
        writer.WriteLine();
        writer.WriteLine("Results");
        writer.WriteLine($"Flux,{Flux.ToString("E6", CultureInfo.InvariantCulture)},mol/(m²·s)");
        writer.WriteLine($"Permeance,{Permeance.ToString("E6", CultureInfo.InvariantCulture)},mol/(m²·Pa·s)");
        writer.WriteLine($"Permeance (GPU),{PermeanceGpu.ToString("F2", CultureInfo.InvariantCulture)},GPU");
        writer.WriteLine();
        writer.WriteLine("Raw Data");
        writer.WriteLine("Time (s),Pressure (Pa)");

        var rangeData = _calculator.Data
            .Where(p => p.Time >= Math.Min(StartTime, EndTime) && p.Time <= Math.Max(StartTime, EndTime))
            .OrderBy(p => p.Time);

        foreach (var (time, pressure) in rangeData)
        {
            writer.WriteLine($"{time.ToString(CultureInfo.InvariantCulture)},{pressure.ToString(CultureInfo.InvariantCulture)}");
        }
    }

    partial void OnStartTimeChanged(double value)
    {
        if (_calculator.Data.Count >= 2)
        {
            try
            {
                StartPressure = _calculator.GetPressureAt(value);
                RangeChanged?.Invoke();
            }
            catch { }
        }
    }

    partial void OnEndTimeChanged(double value)
    {
        if (_calculator.Data.Count >= 2)
        {
            try
            {
                EndPressure = _calculator.GetPressureAt(value);
                RangeChanged?.Invoke();
            }
            catch { }
        }
    }
}
