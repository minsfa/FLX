using System.Globalization;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HVG2020B.Core;
using HVG2020B.Core.Models;
using Microsoft.Win32;

namespace HVG2020B.Viewer.ViewModels;

public record FluxCalculationSnapshot
{
    public required CalculationMode Mode { get; init; }
    // Common
    public required double ChamberVolume { get; init; }
    public required double StartTime { get; init; }
    public required double EndTime { get; init; }
    public required double PressureChangeRate { get; init; }
    public double? RSquared { get; init; }
    public required int DataPointCount { get; init; }
    // Permeation
    public double MembraneArea { get; init; }
    public double Temperature { get; init; }
    public double FeedSidePressure { get; init; }
    public double Flux { get; init; }
    public double Permeance { get; init; }
    public double PermeanceGpu { get; init; }
    // Leak Rate
    public double LeakRateTorrLps { get; init; }
    public double LeakRatePaM3ps { get; init; }
    public double LeakRateMbarLps { get; init; }
    public string ConfigMemo { get; init; } = "";
}

public partial class FluxCalculationViewModel : ObservableObject
{
    private readonly FluxCalculator _calculator;
    private readonly FluxCalculator.Parameters _parameters = new();

    public FluxCalculationViewModel()
    {
        _calculator = new FluxCalculator();
    }

    public string? DefaultExportDirectory { get; set; }
    public FluxCalculationSnapshot? LastCalculation { get; private set; }

    #region Mode

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLeakRateMode))]
    [NotifyPropertyChangedFor(nameof(PermeationVisibility))]
    [NotifyPropertyChangedFor(nameof(LeakRateVisibility))]
    [NotifyPropertyChangedFor(nameof(PressureUnitLabel))]
    [NotifyPropertyChangedFor(nameof(PressureRateUnitLabel))]
    [NotifyPropertyChangedFor(nameof(DisplayStartPressure))]
    [NotifyPropertyChangedFor(nameof(DisplayEndPressure))]
    [NotifyPropertyChangedFor(nameof(DisplayPressureChangeRate))]
    [NotifyPropertyChangedFor(nameof(DisplayLinearizationSlope))]
    private bool _isPermeationMode = true;

    public bool IsLeakRateMode
    {
        get => !IsPermeationMode;
        set => IsPermeationMode = !value;
    }

    public Visibility PermeationVisibility
        => IsPermeationMode ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LeakRateVisibility
        => IsLeakRateMode ? Visibility.Visible : Visibility.Collapsed;

    public string PressureUnitLabel
        => IsPermeationMode ? "Pa" : "Torr";

    public string PressureRateUnitLabel
        => IsPermeationMode ? "Pa/s" : "Torr/s";

    #endregion

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

    [ObservableProperty]
    private string _configMemo = "";

    #endregion

    #region Time Range Selection

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Period))]
    [NotifyPropertyChangedFor(nameof(DisplayStartPressure))]
    [NotifyCanExecuteChangedFor(nameof(CalculateCommand))]
    private double _startTime;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Period))]
    [NotifyPropertyChangedFor(nameof(DisplayEndPressure))]
    [NotifyCanExecuteChangedFor(nameof(CalculateCommand))]
    private double _endTime;

    [ObservableProperty]
    private double _minTime;

    [ObservableProperty]
    private double _maxTime;

    #endregion

    #region Calculated Results — Common

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayStartPressure))]
    private double _startPressure;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayEndPressure))]
    private double _endPressure;

    public double Period => Math.Abs(EndTime - StartTime);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayPressureChangeRate))]
    private double _pressureChangeRate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayLinearizationSlope))]
    private double? _linearizationSlope;

    [ObservableProperty]
    private double? _rSquared;

    [ObservableProperty]
    private int _dataPointCount;

    [ObservableProperty]
    private string _statusMessage = "Load data to begin";

    [ObservableProperty]
    private bool _hasResult;

    #endregion

    #region Calculated Results — Permeation

    [ObservableProperty]
    private double _pressureDrop;

    [ObservableProperty]
    private double _flux;

    [ObservableProperty]
    private double _permeance;

    [ObservableProperty]
    private double _permeanceGpu;

    #endregion

    #region Calculated Results — Leak Rate

    [ObservableProperty]
    private double _leakRateTorrLps;

    [ObservableProperty]
    private double _leakRatePaM3ps;

    [ObservableProperty]
    private double _leakRateMbarLps;

    #endregion

    #region Display Properties (unit-converted for UI)

    public double DisplayStartPressure
        => IsPermeationMode ? StartPressure : StartPressure * FluxCalculator.PaToTorr;

    public double DisplayEndPressure
        => IsPermeationMode ? EndPressure : EndPressure * FluxCalculator.PaToTorr;

    public double DisplayPressureChangeRate
        => IsPermeationMode ? PressureChangeRate : PressureChangeRate * FluxCalculator.PaToTorr;

    public double? DisplayLinearizationSlope
        => IsPermeationMode ? LinearizationSlope : LinearizationSlope * FluxCalculator.PaToTorr;

    #endregion

    #region Chart Data

    public List<double> TimeData { get; } = new();
    public List<double> PressureData { get; } = new();

    public event Action? DataUpdated;
    public event Action? RangeChanged;

    #endregion

    /// <summary>
    /// Loads pressure data from arrays. Data is always stored internally in Pa.
    /// </summary>
    public void LoadData(double[] timeSeconds, double[] pressureTorr)
    {
        _calculator.Clear();
        TimeData.Clear();
        PressureData.Clear();

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
            if (IsPermeationMode)
                CalculatePermeation();
            else
                CalculateLeakRate();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            HasResult = false;
        }
    }

    private void CalculatePermeation()
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

        LastCalculation = new FluxCalculationSnapshot
        {
            Mode = CalculationMode.Permeation,
            ChamberVolume = ChamberVolume,
            StartTime = StartTime,
            EndTime = EndTime,
            PressureChangeRate = PressureChangeRate,
            RSquared = RSquared,
            DataPointCount = DataPointCount,
            MembraneArea = MembraneArea,
            Temperature = Temperature,
            FeedSidePressure = FeedSidePressure,
            Flux = Flux,
            Permeance = Permeance,
            PermeanceGpu = PermeanceGpu
        };

        StatusMessage = $"Calculated ({result.DataPointCount} points, R²={result.RSquared:F4})";
        RangeChanged?.Invoke();
    }

    private void CalculateLeakRate()
    {
        var result = _calculator.CalculateLeakRate(ChamberVolume, StartTime, EndTime);

        StartPressure = result.StartPressure;
        EndPressure = result.EndPressure;
        PressureChangeRate = result.PressureChangeRate;
        LinearizationSlope = result.LinearizationSlope;
        RSquared = result.RSquared;
        DataPointCount = result.DataPointCount;

        LeakRateTorrLps = result.LeakRateTorrLps;
        LeakRatePaM3ps = result.LeakRatePaM3ps;
        LeakRateMbarLps = result.LeakRateMbarLps;
        HasResult = true;

        LastCalculation = new FluxCalculationSnapshot
        {
            Mode = CalculationMode.LeakRate,
            ChamberVolume = ChamberVolume,
            StartTime = StartTime,
            EndTime = EndTime,
            PressureChangeRate = PressureChangeRate,
            RSquared = RSquared,
            DataPointCount = DataPointCount,
            LeakRateTorrLps = LeakRateTorrLps,
            LeakRatePaM3ps = LeakRatePaM3ps,
            LeakRateMbarLps = LeakRateMbarLps,
            ConfigMemo = ConfigMemo
        };

        StatusMessage = $"Leak Rate: {LeakRateTorrLps:E2} Torr·L/s ({DataPointCount} points, R²={RSquared:F4})";
        RangeChanged?.Invoke();
    }

    private bool CanCalculate() => _calculator.Data.Count >= 2 && Period > 0;

    [RelayCommand]
    private void ExportToCsv()
    {
        if (!HasResult) return;

        var modeLabel = IsPermeationMode ? "flux" : "leakrate";
        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = $"{modeLabel}_calculation_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            InitialDirectory = DefaultExportDirectory ?? ""
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                if (IsPermeationMode)
                    ExportPermeationResults(dialog.FileName);
                else
                    ExportLeakRateResults(dialog.FileName);
                StatusMessage = $"Exported to {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
        }
    }

    private void ExportPermeationResults(string filePath)
    {
        using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);

        writer.WriteLine("Parameter,Value,Unit");
        writer.WriteLine($"Mode,Permeation,");
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
        WriteRawData(writer, "Pressure (Pa)", 1.0);
    }

    private void ExportLeakRateResults(string filePath)
    {
        using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);

        writer.WriteLine("Parameter,Value,Unit");
        writer.WriteLine($"Mode,Leak Rate,");
        writer.WriteLine($"Chamber Volume,{ChamberVolume.ToString(CultureInfo.InvariantCulture)},m³");
        if (!string.IsNullOrWhiteSpace(ConfigMemo))
            writer.WriteLine($"Config,{ConfigMemo},");
        writer.WriteLine();
        writer.WriteLine($"Start Time,{StartTime.ToString(CultureInfo.InvariantCulture)},s");
        writer.WriteLine($"End Time,{EndTime.ToString(CultureInfo.InvariantCulture)},s");
        writer.WriteLine($"Period,{Period.ToString(CultureInfo.InvariantCulture)},s");
        writer.WriteLine($"Start Pressure,{DisplayStartPressure.ToString(CultureInfo.InvariantCulture)},Torr");
        writer.WriteLine($"End Pressure,{DisplayEndPressure.ToString(CultureInfo.InvariantCulture)},Torr");
        writer.WriteLine($"Pressure Change Rate,{DisplayPressureChangeRate.ToString(CultureInfo.InvariantCulture)},Torr/s");
        writer.WriteLine($"R-Squared,{RSquared?.ToString(CultureInfo.InvariantCulture) ?? "N/A"},");
        writer.WriteLine();
        writer.WriteLine("Results");
        writer.WriteLine($"Leak Rate,{LeakRateTorrLps.ToString("E6", CultureInfo.InvariantCulture)},Torr·L/s");
        writer.WriteLine($"Leak Rate,{LeakRatePaM3ps.ToString("E6", CultureInfo.InvariantCulture)},Pa·m³/s");
        writer.WriteLine($"Leak Rate,{LeakRateMbarLps.ToString("E6", CultureInfo.InvariantCulture)},mbar·L/s");
        writer.WriteLine();
        WriteRawData(writer, "Pressure (Torr)", FluxCalculator.PaToTorr);
    }

    private void WriteRawData(StreamWriter writer, string pressureHeader, double conversionFactor)
    {
        writer.WriteLine("Raw Data");
        writer.WriteLine($"Time (s),{pressureHeader}");

        var rangeData = _calculator.Data
            .Where(p => p.Time >= Math.Min(StartTime, EndTime) && p.Time <= Math.Max(StartTime, EndTime))
            .OrderBy(p => p.Time);

        foreach (var (time, pressure) in rangeData)
        {
            var convertedPressure = pressure * conversionFactor;
            writer.WriteLine($"{time.ToString(CultureInfo.InvariantCulture)},{convertedPressure.ToString(CultureInfo.InvariantCulture)}");
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
