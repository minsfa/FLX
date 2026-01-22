using Xunit;

namespace HVG2020B.Driver.Tests;

public class PressureParserTests
{
    [Theory]
    [InlineData("1.23E-5 TORR>", 1.23E-5, "TORR", false)]
    [InlineData("1.23E-5 Torr>", 1.23E-5, "Torr", false)]
    [InlineData("1.23e-5 torr>", 1.23E-5, "torr", false)]
    [InlineData("0.001>", 0.001, null, false)]
    [InlineData("0.001 >", 0.001, null, false)]
    [InlineData("  0.001  >", 0.001, null, false)]
    [InlineData("1.5E+02>", 150.0, null, false)]
    [InlineData("1.5E+02 TORR>", 150.0, "TORR", false)]
    public void Parse_TorrValues_ReturnsCorrectPressure(string input, double expectedPressure, string? expectedUnit, bool expectedConverted)
    {
        var result = PressureParser.Parse(input);

        Assert.Equal(expectedPressure, result.PressureTorr, precision: 10);
        Assert.Equal(expectedUnit, result.UnitRaw);
        Assert.Equal(expectedConverted, result.WasConverted);
        Assert.Equal(input, result.RawLine);
    }

    [Theory]
    [InlineData("7.0E+02 mbar >", 525.0434, "mbar")]  // 700 mbar * 0.750062 = 525.0434 Torr
    [InlineData("1.0 mbar>", 0.750062, "mbar")]
    [InlineData("100 Pa>", 0.750062, "Pa")]           // 100 Pa * 0.00750062 = 0.750062 Torr
    [InlineData("1.0 hPa>", 0.750062, "hPa")]         // 1 hPa = 1 mbar
    [InlineData("1.0 kPa>", 7.50062, "kPa")]
    [InlineData("1.0 atm>", 760.0, "atm")]
    [InlineData("1.0 bar>", 750.062, "bar")]
    public void Parse_OtherUnits_ConvertsToTorr(string input, double expectedPressureTorr, string expectedUnit)
    {
        var result = PressureParser.Parse(input);

        Assert.Equal(expectedPressureTorr, result.PressureTorr, precision: 3);
        Assert.Equal(expectedUnit, result.UnitRaw);
        Assert.True(result.WasConverted);
    }

    [Theory]
    [InlineData("1.0 mTORR>", 0.001, "mTORR")]  // milliTorr
    public void Parse_MilliTorr_ConvertsCorrectly(string input, double expectedPressureTorr, string expectedUnit)
    {
        var result = PressureParser.Parse(input);

        Assert.Equal(expectedPressureTorr, result.PressureTorr, precision: 10);
        Assert.Equal(expectedUnit, result.UnitRaw);
        Assert.True(result.WasConverted);
    }

    [Theory]
    [InlineData("-1.5E-3 TORR>", -0.0015)]
    [InlineData("+1.5E-3 TORR>", 0.0015)]
    public void Parse_SignedValues_ParsesCorrectly(string input, double expectedPressure)
    {
        var result = PressureParser.Parse(input);

        Assert.Equal(expectedPressure, result.PressureTorr, precision: 10);
    }

    [Theory]
    [InlineData("1.23E-5")]           // No prompt
    [InlineData("1.23E-5 TORR")]      // No prompt
    [InlineData("1.23E-5>")]          // Works without space before >
    public void Parse_VariousFormats_ParsesSuccessfully(string input)
    {
        var result = PressureParser.Parse(input);

        Assert.True(result.PressureTorr > 0 || result.PressureTorr < 0 || result.PressureTorr == 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(">")]
    [InlineData("TORR>")]
    [InlineData("abc>")]
    [InlineData("not a number")]
    public void Parse_InvalidInput_ThrowsParseException(string input)
    {
        Assert.Throws<HVGParseException>(() => PressureParser.Parse(input));
    }

    [Fact]
    public void Parse_NullInput_ThrowsParseException()
    {
        Assert.Throws<HVGParseException>(() => PressureParser.Parse(null!));
    }

    [Theory]
    [InlineData("1.0 unknownunit>", 1.0, "unknownunit")]  // Unknown unit treated as Torr
    public void Parse_UnknownUnit_TreatsAsTorr(string input, double expectedPressure, string expectedUnit)
    {
        var result = PressureParser.Parse(input);

        Assert.Equal(expectedPressure, result.PressureTorr, precision: 10);
        Assert.Equal(expectedUnit, result.UnitRaw);
        Assert.False(result.WasConverted);  // Not converted because unit wasn't recognized
    }

    [Fact]
    public void TryParse_ValidInput_ReturnsTrue()
    {
        var success = PressureParser.TryParse("1.23E-5 TORR>", out var reading, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(1.23E-5, reading.PressureTorr, precision: 10);
    }

    [Fact]
    public void TryParse_InvalidInput_ReturnsFalse()
    {
        var success = PressureParser.TryParse("invalid>", out var reading, out var error);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Equal(default, reading);
    }

    [Fact]
    public void RecognizedUnits_ContainsExpectedUnits()
    {
        var units = PressureParser.RecognizedUnits;

        Assert.Contains("TORR", units);
        Assert.Contains("mbar", units);
        Assert.Contains("Pa", units);
        Assert.Contains("atm", units);
    }

    [Theory]
    [InlineData("  7.0E+02 mbar >")]  // From requirements
    public void Parse_RequirementExamples_ParsesCorrectly(string input)
    {
        var result = PressureParser.Parse(input);

        // 700 mbar = 700 * 0.750062 = 525.0434 Torr
        Assert.Equal(525.0434, result.PressureTorr, precision: 2);
        Assert.Equal("mbar", result.UnitRaw);
        Assert.True(result.WasConverted);
    }
}
