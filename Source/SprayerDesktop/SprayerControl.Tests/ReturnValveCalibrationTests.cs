using System.Collections;
using SprayerControl.Core;

namespace SprayerControl.Tests;

public sealed class ReturnValveCalibrationTests
{
    [Fact]
    public void ConstructorSortsAnImmutableSnapshotOfAllTenRequiredPoints()
    {
        List<CalibrationPoint> source = ValidPoints().AsEnumerable().Reverse().ToList();

        ReturnValveCalibration calibration = new(source);
        source[0] = new CalibrationPoint(100, 19.9);

        Assert.Equal(Enumerable.Range(1, 10).Select(index => index * 10), calibration.Points.Select(point => point.OpeningPercent));
        Assert.Equal(0.1, calibration.Points[0].CurrentMilliamps, precision: 6);

        IList exposed = Assert.IsAssignableFrom<IList>(calibration.Points);
        Assert.Throws<NotSupportedException>(() => exposed[0] = new CalibrationPoint(10, 19.9));
    }

    [Fact]
    public void ConstructorRejectsNullInput()
    {
        Assert.Throws<ArgumentNullException>(() => new ReturnValveCalibration(null!));
    }

    [Fact]
    public void ConstructorRejectsNullElementsWithAControlledArgumentException()
    {
        List<CalibrationPoint> points = ValidPoints();
        points[4] = null!;

        Assert.Throws<ArgumentException>(() => new ReturnValveCalibration(points));
    }

    [Theory]
    [MemberData(nameof(InvalidPointCollections))]
    public void ConstructorRejectsInvalidPointCollections(IReadOnlyList<CalibrationPoint> points)
    {
        Assert.Throws<ArgumentException>(() => new ReturnValveCalibration(points));
    }

    [Fact]
    public void ConstructorAcceptsFlatAndDecreasingCurvesButRejectsDirectionReversal()
    {
        IReadOnlyList<CalibrationPoint> flat = Enumerable.Range(1, 10)
            .Select(index => new CalibrationPoint(index * 10, 5.0))
            .ToList();
        IReadOnlyList<CalibrationPoint> decreasing = Enumerable.Range(1, 10)
            .Select(index => new CalibrationPoint(index * 10, 10.0 - (index * 0.5)))
            .ToList();
        IReadOnlyList<CalibrationPoint> reversal =
        [
            new(10, 1), new(20, 2), new(30, 3), new(40, 2), new(50, 2),
            new(60, 2), new(70, 2), new(80, 2), new(90, 2), new(100, 2),
        ];

        Assert.Equal(5.0, new ReturnValveCalibration(flat).CurrentMilliampsAt(55));
        Assert.Equal(7.25, new ReturnValveCalibration(decreasing).CurrentMilliampsAt(55), precision: 6);
        Assert.Throws<ArgumentException>(() => new ReturnValveCalibration(reversal));
    }

    [Fact]
    public void CurrentLookupSupportsExactPointsAndLinearInterpolation()
    {
        ReturnValveCalibration calibration = new(ValidPoints());

        Assert.Equal(0.2, calibration.CurrentMilliampsAt(20), precision: 6);
        Assert.Equal(0.35, calibration.CurrentMilliampsAt(35), precision: 6);
        Assert.Equal(1.0, calibration.CurrentMilliampsAt(100), precision: 6);
    }

    [Fact]
    public void CurrentLookupAcceptsInclusiveCurrentEndpointsAndTheLowerOpeningBoundary()
    {
        ReturnValveCalibration zero = new(Enumerable.Range(1, 10)
            .Select(index => new CalibrationPoint(index * 10, 0.0))
            .ToList());
        ReturnValveCalibration twenty = new(Enumerable.Range(1, 10)
            .Select(index => new CalibrationPoint(index * 10, 20.0))
            .ToList());

        Assert.Equal(0.0, zero.CurrentMilliampsAt(10));
        Assert.Equal(20.0, twenty.CurrentMilliampsAt(10));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(9.999)]
    [InlineData(100.001)]
    public void CurrentLookupRejectsNonFiniteAndOutOfDomainOpenings(double openingPercent)
    {
        ReturnValveCalibration calibration = new(ValidPoints());

        Assert.Throws<ArgumentOutOfRangeException>(() => calibration.CurrentMilliampsAt(openingPercent));
    }

    [Fact]
    public void SafetyAwareLookupRejectsInvalidSafetyMinimumAndOpeningBelowIt()
    {
        ReturnValveCalibration calibration = new(ValidPoints());

        Assert.Throws<ArgumentOutOfRangeException>(() => calibration.CurrentMilliampsAt(50, 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => calibration.CurrentMilliampsAt(50, 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => calibration.CurrentMilliampsAt(double.NaN, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() => calibration.CurrentMilliampsAt(101, 30));
        Assert.Throws<InvalidOperationException>(() => calibration.CurrentMilliampsAt(20, safetyMinimumOpening: 30));
        Assert.Equal(0.35, calibration.CurrentMilliampsAt(35, safetyMinimumOpening: 30), precision: 6);
    }

    public static IEnumerable<object[]> InvalidPointCollections()
    {
        yield return [ValidPoints().Take(9).ToList()];
        yield return [ValidPoints().Append(new CalibrationPoint(100, 1.1)).ToList()];
        yield return [ValidPoints().Select(point => point.OpeningPercent == 30 ? new CalibrationPoint(35, point.CurrentMilliamps) : point).ToList()];
        yield return [ValidPoints().Select(point => point.OpeningPercent == 30 ? new CalibrationPoint(20, point.CurrentMilliamps) : point).ToList()];
        yield return [ValidPoints().Select(point => point.OpeningPercent == 20 ? new CalibrationPoint(20, double.NaN) : point).ToList()];
        yield return [ValidPoints().Select(point => point.OpeningPercent == 20 ? new CalibrationPoint(20, double.PositiveInfinity) : point).ToList()];
        yield return [ValidPoints().Select(point => point.OpeningPercent == 20 ? new CalibrationPoint(20, double.NegativeInfinity) : point).ToList()];
        yield return [ValidPoints().Select(point => point.OpeningPercent == 20 ? new CalibrationPoint(20, -0.01) : point).ToList()];
        yield return [ValidPoints().Select(point => point.OpeningPercent == 20 ? new CalibrationPoint(20, 20.01) : point).ToList()];
    }

    internal static List<CalibrationPoint> ValidPoints() => Enumerable.Range(1, 10)
        .Select(index => new CalibrationPoint(index * 10, index / 10.0))
        .ToList();
}
