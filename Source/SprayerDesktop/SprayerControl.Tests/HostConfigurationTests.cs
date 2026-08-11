using System.Text.Json;
using SprayerControl.Core;

namespace SprayerControl.Tests;

public sealed class HostConfigurationTests
{
    [Fact]
    public void ConstructorExposesAnIndependentImmutableCalibrationSnapshot()
    {
        ReturnValveCalibration input = new(ReturnValveCalibrationTests.ValidPoints());
        HostConfiguration configuration = new(1, 30, 10, input, DateTimeOffset.UtcNow);

        Assert.NotSame(input, configuration.ReturnValveCalibration);
        Assert.Equal(input.Points, configuration.ReturnValveCalibration.Points);
        Assert.Throws<NotSupportedException>(() =>
            ((System.Collections.IList)configuration.ReturnValveCalibration.Points)[0] = new CalibrationPoint(10, 19.9));
    }

    [Theory]
    [InlineData(0, 30, 10)]
    [InlineData(1, 29, 10)]
    [InlineData(1, 81, 10)]
    [InlineData(1, 30, 9)]
    [InlineData(1, 30, 101)]
    public void ConstructorRejectsOutOfRangeConfigurationValues(int schemaVersion, int pwmDutyMinimum, int safetyMinimum)
    {
        ReturnValveCalibration calibration = new(ReturnValveCalibrationTests.ValidPoints());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HostConfiguration(schemaVersion, pwmDutyMinimum, safetyMinimum, calibration, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ConstructorRejectsMissingTimestampAndCalibration()
    {
        ReturnValveCalibration calibration = new(ReturnValveCalibrationTests.ValidPoints());

        Assert.Throws<ArgumentException>(() => new HostConfiguration(1, 30, 10, calibration, default));
        Assert.Throws<ArgumentNullException>(() => new HostConfiguration(1, 30, 10, null!, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ConstructorNormalizesNonDefaultTimestampToUtc()
    {
        DateTimeOffset localTimestamp = new(2026, 8, 12, 18, 30, 0, TimeSpan.FromHours(8));

        HostConfiguration configuration = new(
            1,
            30,
            10,
            new ReturnValveCalibration(ReturnValveCalibrationTests.ValidPoints()),
            localTimestamp);

        Assert.Equal(localTimestamp.ToUniversalTime(), configuration.LastModifiedUtc);
        Assert.Equal(TimeSpan.Zero, configuration.LastModifiedUtc.Offset);
    }

    [Fact]
    public void DefaultJsonSerializerRoundTripsConfigurationAndCalibration()
    {
        HostConfiguration original = new(
            2,
            45,
            30,
            new ReturnValveCalibration(ReturnValveCalibrationTests.ValidPoints()),
            new DateTimeOffset(2026, 8, 12, 18, 30, 0, TimeSpan.FromHours(8)));

        string json = JsonSerializer.Serialize(original);
        HostConfiguration roundTripped = Assert.IsType<HostConfiguration>(JsonSerializer.Deserialize<HostConfiguration>(json));

        Assert.Equal(original.SchemaVersion, roundTripped.SchemaVersion);
        Assert.Equal(original.PwmDutyMinimum, roundTripped.PwmDutyMinimum);
        Assert.Equal(original.ReturnValveSafetyMinimum, roundTripped.ReturnValveSafetyMinimum);
        Assert.Equal(original.LastModifiedUtc, roundTripped.LastModifiedUtc);
        Assert.Equal(original.ReturnValveCalibration.Points, roundTripped.ReturnValveCalibration.Points);
    }

    [Fact]
    public void ConfigurationRetainsValidatedCalibrationBehavior()
    {
        HostConfiguration configuration = new(
            schemaVersion: 1,
            pwmDutyMinimum: 45,
            returnValveSafetyMinimum: 30,
            returnValveCalibration: new ReturnValveCalibration(ReturnValveCalibrationTests.ValidPoints()),
            lastModifiedUtc: DateTimeOffset.UtcNow);

        Assert.Equal(1, configuration.SchemaVersion);
        Assert.Equal(45, configuration.PwmDutyMinimum);
        Assert.Equal(30, configuration.ReturnValveSafetyMinimum);
        Assert.Equal(0.35, configuration.ReturnValveCalibration.CurrentMilliampsAt(35, configuration.ReturnValveSafetyMinimum), precision: 6);
    }
}
