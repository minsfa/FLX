using System.Globalization;
using System.Text.RegularExpressions;

namespace HVG2020B.Driver;

/// <summary>
/// Parses HVG-2020B pressure responses.
/// </summary>
public static partial class PressureParser
{
    // Conversion factors to Torr
    private static readonly Dictionary<string, double> UnitConversions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TORR"] = 1.0,
        ["mTORR"] = 0.001,
        ["mbar"] = 0.750062,      // 1 mbar = 0.750062 Torr
        ["Pa"] = 0.00750062,      // 1 Pa = 0.00750062 Torr
        ["hPa"] = 0.750062,       // 1 hPa = 1 mbar = 0.750062 Torr
        ["kPa"] = 7.50062,        // 1 kPa = 7.50062 Torr
        ["atm"] = 760.0,          // 1 atm = 760 Torr
        ["bar"] = 750.062,        // 1 bar = 750.062 Torr
        ["PSI"] = 51.7149,        // 1 PSI = 51.7149 Torr
    };

    // Regex to extract numeric value (including scientific notation) and optional unit
    // Examples: "1.23E-5 TORR>", "0.001>", "7.0E+02 mbar >"
    [GeneratedRegex(@"^\s*([+-]?\d+\.?\d*(?:[eE][+-]?\d+)?)\s*(\w+)?\s*>?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ResponsePattern();

    /// <summary>
    /// Parses a raw response line from the HVG-2020B.
    /// </summary>
    /// <param name="rawLine">Raw response (may include '>' prompt)</param>
    /// <returns>Parsed pressure reading</returns>
    /// <exception cref="HVGParseException">If parsing fails</exception>
    public static PressureReading Parse(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            throw new HVGParseException(rawLine ?? "", "Response is empty");
        }

        // Remove trailing '>' if present
        var cleaned = rawLine.TrimEnd().TrimEnd('>').Trim();

        var match = ResponsePattern().Match(cleaned + ">");
        if (!match.Success)
        {
            throw new HVGParseException(rawLine, "Could not extract numeric value");
        }

        var numericStr = match.Groups[1].Value;
        var unitStr = match.Groups[2].Success ? match.Groups[2].Value : null;

        if (!double.TryParse(numericStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericValue))
        {
            throw new HVGParseException(rawLine, $"Invalid numeric value: '{numericStr}'");
        }

        // Convert to Torr if unit is recognized
        double pressureTorr;
        bool wasConverted = false;

        if (unitStr != null && UnitConversions.TryGetValue(unitStr, out var conversionFactor))
        {
            pressureTorr = numericValue * conversionFactor;
            wasConverted = !unitStr.Equals("TORR", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // No unit or unrecognized unit - assume Torr
            pressureTorr = numericValue;
        }

        return new PressureReading
        {
            PressureTorr = pressureTorr,
            UnitRaw = unitStr,
            RawLine = rawLine,
            WasConverted = wasConverted
        };
    }

    /// <summary>
    /// Attempts to parse a raw response line.
    /// </summary>
    /// <param name="rawLine">Raw response</param>
    /// <param name="reading">Parsed reading if successful</param>
    /// <param name="error">Error message if parsing failed</param>
    /// <returns>True if parsing succeeded</returns>
    public static bool TryParse(string rawLine, out PressureReading reading, out string? error)
    {
        try
        {
            reading = Parse(rawLine);
            error = null;
            return true;
        }
        catch (HVGParseException ex)
        {
            reading = default;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Gets list of recognized units.
    /// </summary>
    public static IReadOnlyCollection<string> RecognizedUnits => UnitConversions.Keys;
}
