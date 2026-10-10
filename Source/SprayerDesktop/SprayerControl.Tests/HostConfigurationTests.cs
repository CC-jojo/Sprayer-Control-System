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
    [InlineData(1, 101, 10)]
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
            new DateTimeOffset(2026, 8, 12, 18, 30, 0, TimeSpan.FromHours(8)),
            lastDutyPercent: 66);

        string json = JsonSerializer.Serialize(original);
        HostConfiguration roundTripped = Assert.IsType<HostConfiguration>(JsonSerializer.Deserialize<HostConfiguration>(json));

        Assert.Equal(original.SchemaVersion, roundTripped.SchemaVersion);
        Assert.Equal(original.PwmDutyMinimum, roundTripped.PwmDutyMinimum);
        Assert.Equal(original.ReturnValveSafetyMinimum, roundTripped.ReturnValveSafetyMinimum);
        Assert.Equal(original.LastModifiedUtc, roundTripped.LastModifiedUtc);
        Assert.Equal(original.ReturnValveCalibration.Points, roundTripped.ReturnValveCalibration.Points);
        Assert.Equal(original.NozzleMapping, roundTripped.NozzleMapping);
        Assert.Equal(66, roundTripped.LastDutyPercent);
    }

    [Fact]
    public void LastDutyPercentDefaultsToNullAndValidatesRange()
    {
        HostConfiguration configuration = new(
            1, 30, 10,
            new ReturnValveCalibration(ReturnValveCalibrationTests.ValidPoints()),
            DateTimeOffset.UtcNow);

        Assert.Null(configuration.LastDutyPercent);

        HostConfiguration remembered = new(
            1, 30, 10,
            new ReturnValveCalibration(ReturnValveCalibrationTests.ValidPoints()),
            DateTimeOffset.UtcNow,
            lastDutyPercent: 55);

        Assert.Equal(55, remembered.LastDutyPercent);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HostConfiguration(
                1, 30, 10,
                new ReturnValveCalibration(ReturnValveCalibrationTests.ValidPoints()),
                DateTimeOffset.UtcNow,
                lastDutyPercent: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HostConfiguration(
                1, 30, 10,
                new ReturnValveCalibration(ReturnValveCalibrationTests.ValidPoints()),
                DateTimeOffset.UtcNow,
                lastDutyPercent: 101));
    }

    [Fact]
    public void DefaultMappingIsAppliedWhenNozzleMappingIsOmitted()
    {
        HostConfiguration configuration = new(1, 30, 10, new ReturnValveCalibration(ReturnValveCalibrationTests.ValidPoints()), DateTimeOffset.UtcNow);

        Assert.Equal(NozzleMask.DefaultUiToPhysicalBit, configuration.NozzleMapping);
        Assert.NotSame(NozzleMask.DefaultUiToPhysicalBit, configuration.NozzleMapping);
    }

    [Fact]
    public void CustomMappingIsClonedAndExposed()
    {
        byte[] mapping = [0, 1, 2, 3, 4, 5, 6, 7];
        HostConfiguration configuration = new(
            1, 30, 10,
            new ReturnValveCalibration(ReturnValveCalibrationTests.ValidPoints()),
            DateTimeOffset.UtcNow,
            mapping);

        Assert.Equal(mapping, configuration.NozzleMapping);
        Assert.NotSame(mapping, configuration.NozzleMapping);
        mapping[0] = 7;
        Assert.Equal(0, configuration.NozzleMapping[0]);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0, 1, 2, 3, 4, 5, 6 })]
    [InlineData(new byte[] { 0, 1, 2, 3, 4, 5, 6, 8 })]
    [InlineData(new byte[] { 0, 1, 2, 3, 4, 5, 6, 6 })]
    public void InvalidMappingIsRejected(byte[] mapping)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new HostConfiguration(1, 30, 10, new ReturnValveCalibration(ReturnValveCalibrationTests.ValidPoints()), DateTimeOffset.UtcNow, mapping));
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

    [Fact]
    public void PowerOnReturnValveDefaultsToDisabledWithFullOpenSetpoint()
    {
        HostConfiguration configuration = new(
            1, 30, 30,
            new ReturnValveCalibration(ReturnValveCalibrationTests.ValidPoints()),
            DateTimeOffset.UtcNow);

        Assert.False(configuration.PowerOnReturnValveEnabled);
        Assert.Equal(100, configuration.PowerOnReturnValvePercent);
        Assert.Null(configuration.LastReturnOpeningPercent);
    }

    [Fact]
    public void PowerOnReturnValveAndRememberedOpeningAreClampedToTheSafetyFloor()
    {
        // 安全下限 60%：低于下限的设定值与记忆值都抬到下限。
        HostConfiguration configuration = new(
            1, 30, 60,
            new ReturnValveCalibration(ReturnValveCalibrationTests.ValidPoints()),
            DateTimeOffset.UtcNow,
            powerOnReturnValveEnabled: true,
            powerOnReturnValvePercent: 40,
            lastReturnOpeningPercent: 15);

        Assert.True(configuration.PowerOnReturnValveEnabled);
        Assert.Equal(60, configuration.PowerOnReturnValvePercent);
        Assert.Equal(60d, configuration.LastReturnOpeningPercent!.Value);

        // 超过 100% 的一律压回 100%。
        HostConfiguration aboveMaximum = new(
            1, 30, 30,
            new ReturnValveCalibration(ReturnValveCalibrationTests.ValidPoints()),
            DateTimeOffset.UtcNow,
            powerOnReturnValvePercent: 130,
            lastReturnOpeningPercent: 140);

        Assert.Equal(100, aboveMaximum.PowerOnReturnValvePercent);
        Assert.Equal(100d, aboveMaximum.LastReturnOpeningPercent!.Value);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HostConfiguration(
                1, 30, 30,
                new ReturnValveCalibration(ReturnValveCalibrationTests.ValidPoints()),
                DateTimeOffset.UtcNow,
                lastReturnOpeningPercent: double.NaN));
    }

    [Fact]
    public void PowerOnReturnValveSettingsSurviveJsonRoundTrip()
    {
        HostConfiguration original = new(
            1, 30, 20,
            new ReturnValveCalibration(ReturnValveCalibrationTests.ValidPoints()),
            DateTimeOffset.UtcNow,
            powerOnReturnValveEnabled: true,
            powerOnReturnValvePercent: 75,
            lastReturnOpeningPercent: 42.5);

        string json = JsonSerializer.Serialize(original);
        HostConfiguration roundTripped = Assert.IsType<HostConfiguration>(JsonSerializer.Deserialize<HostConfiguration>(json));

        Assert.True(roundTripped.PowerOnReturnValveEnabled);
        Assert.Equal(75, roundTripped.PowerOnReturnValvePercent);
        Assert.Equal(42.5d, roundTripped.LastReturnOpeningPercent!.Value);
    }
}
