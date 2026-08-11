using System.Collections.ObjectModel;

namespace SprayerControl.Core;

public sealed record CalibrationPoint(int OpeningPercent, double CurrentMilliamps);

public sealed class ReturnValveCalibration
{
    private const int MinimumOpening = 10;
    private const int MaximumOpening = 100;
    private const int OpeningIncrement = 10;
    private readonly ReadOnlyCollection<CalibrationPoint> points;

    public ReturnValveCalibration(IReadOnlyList<CalibrationPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        CalibrationPoint[] snapshot = points.ToArray();
        if (Array.Exists(snapshot, static point => point is null))
        {
            throw new ArgumentException("Calibration points cannot contain null elements.", nameof(points));
        }

        CalibrationPoint[] sortedPoints = snapshot.OrderBy(point => point.OpeningPercent).ToArray();
        ValidatePoints(sortedPoints);
        this.points = Array.AsReadOnly(sortedPoints);
    }

    public IReadOnlyList<CalibrationPoint> Points => points;

    public double CurrentMilliampsAt(double openingPercent)
    {
        ValidateOpening(openingPercent);

        int upperIndex = (int)Math.Ceiling(openingPercent / OpeningIncrement) - 1;
        int lowerIndex = (int)Math.Floor(openingPercent / OpeningIncrement) - 1;

        CalibrationPoint lower = points[lowerIndex];
        CalibrationPoint upper = points[upperIndex];
        if (lowerIndex == upperIndex)
        {
            return lower.CurrentMilliamps;
        }

        double fraction = (openingPercent - lower.OpeningPercent) /
            (upper.OpeningPercent - lower.OpeningPercent);
        return lower.CurrentMilliamps + ((upper.CurrentMilliamps - lower.CurrentMilliamps) * fraction);
    }

    public double CurrentMilliampsAt(double openingPercent, int safetyMinimumOpening)
    {
        if (safetyMinimumOpening is < MinimumOpening or > MaximumOpening)
        {
            throw new ArgumentOutOfRangeException(nameof(safetyMinimumOpening), safetyMinimumOpening, "Safety minimum opening must be from 10 through 100.");
        }

        ValidateOpening(openingPercent);
        if (openingPercent < safetyMinimumOpening)
        {
            throw new InvalidOperationException("Requested opening is below the configured safety minimum.");
        }

        return CurrentMilliampsAt(openingPercent);
    }

    private static void ValidatePoints(IReadOnlyList<CalibrationPoint> candidatePoints)
    {
        if (candidatePoints.Count != 10)
        {
            throw new ArgumentException("Calibration must contain exactly ten points.", nameof(candidatePoints));
        }

        for (int index = 0; index < candidatePoints.Count; index++)
        {
            CalibrationPoint point = candidatePoints[index];
            int expectedOpening = MinimumOpening + (index * OpeningIncrement);
            if (point.OpeningPercent != expectedOpening)
            {
                throw new ArgumentException("Calibration openings must be 10 through 100 in increments of 10.", nameof(candidatePoints));
            }

            if (!double.IsFinite(point.CurrentMilliamps) || point.CurrentMilliamps is < 0 or > 20)
            {
                throw new ArgumentException("Calibration current must be finite and within 0 through 20 mA.", nameof(candidatePoints));
            }
        }

        int direction = 0;
        for (int index = 1; index < candidatePoints.Count; index++)
        {
            int comparison = candidatePoints[index].CurrentMilliamps.CompareTo(candidatePoints[index - 1].CurrentMilliamps);
            if (comparison == 0)
            {
                continue;
            }

            if (direction != 0 && comparison != direction)
            {
                throw new ArgumentException("Calibration current must be monotonic.", nameof(candidatePoints));
            }

            direction = comparison;
        }
    }

    private static void ValidateOpening(double openingPercent)
    {
        if (!double.IsFinite(openingPercent) || openingPercent < MinimumOpening || openingPercent > MaximumOpening)
        {
            throw new ArgumentOutOfRangeException(nameof(openingPercent), openingPercent, "Opening must be finite and within 10 through 100.");
        }
    }
}
