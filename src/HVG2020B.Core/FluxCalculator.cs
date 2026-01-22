using MathNet.Numerics;
using MathNet.Numerics.Interpolation;

namespace HVG2020B.Core;

/// <summary>
/// Calculates permeance and flux from pressure time-series data.
/// Based on the ideal gas law: Flux = (dP/dt) × V / (A × T × R)
/// </summary>
public class FluxCalculator
{
    /// <summary>
    /// Universal gas constant in J/(mol·K)
    /// </summary>
    public const double GasConstant = 8.314;

    /// <summary>
    /// Conversion factor from mol/(m²·Pa·s) to GPU
    /// </summary>
    public const double GpuConversionFactor = 3.348e-10;

    /// <summary>
    /// Input parameters for flux calculation.
    /// </summary>
    public class Parameters
    {
        /// <summary>
        /// Membrane area in m²
        /// </summary>
        public double MembraneArea { get; set; } = 1e-4; // 1 cm²

        /// <summary>
        /// Temperature in Kelvin
        /// </summary>
        public double Temperature { get; set; } = 298.15; // 25°C

        /// <summary>
        /// Feed side pressure in Pa
        /// </summary>
        public double FeedSidePressure { get; set; } = 101325; // 1 atm

        /// <summary>
        /// Chamber volume in m³
        /// </summary>
        public double ChamberVolume { get; set; } = 1e-6; // 1 cm³
    }

    /// <summary>
    /// Result of flux calculation.
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Start time of the selected range in seconds
        /// </summary>
        public double StartTime { get; set; }

        /// <summary>
        /// End time of the selected range in seconds
        /// </summary>
        public double EndTime { get; set; }

        /// <summary>
        /// Start pressure value in Pa
        /// </summary>
        public double StartPressure { get; set; }

        /// <summary>
        /// End pressure value in Pa
        /// </summary>
        public double EndPressure { get; set; }

        /// <summary>
        /// Time period of the selected range in seconds
        /// </summary>
        public double Period => Math.Abs(EndTime - StartTime);

        /// <summary>
        /// Simple pressure change rate in Pa/s: (EndPressure - StartPressure) / Period
        /// </summary>
        public double PressureChangeRate { get; set; }

        /// <summary>
        /// Linearization slope from linear regression in Pa/s (more accurate)
        /// </summary>
        public double? LinearizationSlope { get; set; }

        /// <summary>
        /// Effective pressure change rate (uses linearization if available)
        /// </summary>
        public double EffectivePressureChangeRate => LinearizationSlope ?? PressureChangeRate;

        /// <summary>
        /// Pressure drop across membrane in Pa: FeedSidePressure - AveragePressure
        /// </summary>
        public double PressureDrop { get; set; }

        /// <summary>
        /// Flux in mol/(m²·s)
        /// </summary>
        public double Flux { get; set; }

        /// <summary>
        /// Permeance in mol/(m²·Pa·s)
        /// </summary>
        public double Permeance { get; set; }

        /// <summary>
        /// Permeance in GPU (Gas Permeation Unit)
        /// </summary>
        public double PermeanceGpu => Permeance / GpuConversionFactor;

        /// <summary>
        /// Number of data points used in calculation
        /// </summary>
        public int DataPointCount { get; set; }

        /// <summary>
        /// R-squared value from linear regression (0-1, higher is better fit)
        /// </summary>
        public double? RSquared { get; set; }
    }

    private readonly List<(double Time, double Pressure)> _data = new();
    private IInterpolation? _interpolation;

    /// <summary>
    /// Gets the data points.
    /// </summary>
    public IReadOnlyList<(double Time, double Pressure)> Data => _data;

    /// <summary>
    /// Clears all data.
    /// </summary>
    public void Clear()
    {
        _data.Clear();
        _interpolation = null;
    }

    /// <summary>
    /// Adds a data point.
    /// </summary>
    /// <param name="timeSeconds">Time in seconds</param>
    /// <param name="pressurePa">Pressure in Pa</param>
    public void AddDataPoint(double timeSeconds, double pressurePa)
    {
        _data.Add((timeSeconds, pressurePa));
        _interpolation = null; // Invalidate interpolation
    }

    /// <summary>
    /// Adds multiple data points.
    /// </summary>
    public void AddDataPoints(IEnumerable<(double Time, double Pressure)> points)
    {
        _data.AddRange(points);
        _interpolation = null;
    }

    /// <summary>
    /// Loads data from time and pressure arrays.
    /// </summary>
    public void LoadData(double[] timeSeconds, double[] pressurePa)
    {
        if (timeSeconds.Length != pressurePa.Length)
            throw new ArgumentException("Time and pressure arrays must have the same length");

        Clear();
        for (int i = 0; i < timeSeconds.Length; i++)
        {
            _data.Add((timeSeconds[i], pressurePa[i]));
        }
    }

    /// <summary>
    /// Calculates flux and permeance for the specified time range.
    /// </summary>
    /// <param name="parameters">Calculation parameters</param>
    /// <param name="startTime">Start time in seconds</param>
    /// <param name="endTime">End time in seconds</param>
    /// <returns>Calculation result</returns>
    public Result Calculate(Parameters parameters, double startTime, double endTime)
    {
        if (_data.Count < 2)
            throw new InvalidOperationException("At least 2 data points are required");

        // Ensure interpolation is built
        EnsureInterpolation();

        // Get interpolated values at boundaries
        double startPressure = _interpolation!.Interpolate(startTime);
        double endPressure = _interpolation.Interpolate(endTime);

        // Calculate period
        double period = Math.Abs(endTime - startTime);
        if (period <= 0)
            throw new ArgumentException("Start and end time must be different");

        // Simple pressure change rate
        double pressureChangeRate = (endPressure - startPressure) / period;

        // Get data points within range for linear regression
        var rangeData = GetDataInRange(startTime, endTime);

        // Linear regression for more accurate slope
        double? linearizationSlope = null;
        double? rSquared = null;

        if (rangeData.Count >= 2)
        {
            var times = rangeData.Select(p => p.Time).ToArray();
            var pressures = rangeData.Select(p => p.Pressure).ToArray();

            var (intercept, slope) = Fit.Line(times, pressures);
            linearizationSlope = startTime < endTime ? slope : -slope;

            // Calculate R-squared
            rSquared = GoodnessOfFit.RSquared(
                times.Select(t => intercept + slope * t),
                pressures);
        }

        // Use linearization slope if available, otherwise simple rate
        double effectiveRate = linearizationSlope ?? pressureChangeRate;

        // Pressure drop: FeedSidePressure - average permeate pressure
        double averagePressure = (startPressure + endPressure) / 2;
        double pressureDrop = parameters.FeedSidePressure - averagePressure;

        // Flux calculation: (dP/dt) × V / (A × T × R)
        double flux = effectiveRate * parameters.ChamberVolume /
                      (parameters.MembraneArea * parameters.Temperature * GasConstant);

        // Permeance: Flux / PressureDrop
        double permeance = pressureDrop > 0 ? flux / pressureDrop : 0;

        return new Result
        {
            StartTime = startTime,
            EndTime = endTime,
            StartPressure = startPressure,
            EndPressure = endPressure,
            PressureChangeRate = pressureChangeRate,
            LinearizationSlope = linearizationSlope,
            PressureDrop = pressureDrop,
            Flux = flux,
            Permeance = permeance,
            DataPointCount = rangeData.Count,
            RSquared = rSquared
        };
    }

    /// <summary>
    /// Gets interpolated pressure value at the specified time.
    /// </summary>
    public double GetPressureAt(double timeSeconds)
    {
        EnsureInterpolation();
        return _interpolation!.Interpolate(timeSeconds);
    }

    private void EnsureInterpolation()
    {
        if (_interpolation == null && _data.Count >= 2)
        {
            var sortedData = _data.OrderBy(p => p.Time).ToList();
            _interpolation = Interpolate.Linear(
                sortedData.Select(p => p.Time),
                sortedData.Select(p => p.Pressure));
        }
    }

    private List<(double Time, double Pressure)> GetDataInRange(double startTime, double endTime)
    {
        double minTime = Math.Min(startTime, endTime);
        double maxTime = Math.Max(startTime, endTime);

        return _data
            .Where(p => p.Time >= minTime && p.Time <= maxTime)
            .OrderBy(p => p.Time)
            .ToList();
    }
}
