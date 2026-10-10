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
            throw new ArgumentException("标定点不能包含空元素。", nameof(points));
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
            throw new ArgumentOutOfRangeException(nameof(safetyMinimumOpening), safetyMinimumOpening, "安全下限开度必须在 10 到 100 之间。");
        }

        ValidateOpening(openingPercent);
        if (openingPercent < safetyMinimumOpening)
        {
            throw new InvalidOperationException("请求的开度低于配置的安全下限。");
        }

        return CurrentMilliampsAt(openingPercent);
    }

    private static void ValidatePoints(IReadOnlyList<CalibrationPoint> candidatePoints)
    {
        if (candidatePoints.Count != 10)
        {
            throw new ArgumentException("标定必须包含十个标定点。", nameof(candidatePoints));
        }

        for (int index = 0; index < candidatePoints.Count; index++)
        {
            CalibrationPoint point = candidatePoints[index];
            int expectedOpening = MinimumOpening + (index * OpeningIncrement);
            if (point.OpeningPercent != expectedOpening)
            {
                throw new ArgumentException("标定开度必须是 10 到 100，且以 10 递增。", nameof(candidatePoints));
            }

            if (!double.IsFinite(point.CurrentMilliamps) || point.CurrentMilliamps is < 0 or > 20)
            {
                throw new ArgumentException("标定电流必须是有限值，且在 0 到 20 mA 之间。", nameof(candidatePoints));
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
                throw new ArgumentException("标定电流必须是单调的。", nameof(candidatePoints));
            }

            direction = comparison;
        }
    }

    private static void ValidateOpening(double openingPercent)
    {
        if (!double.IsFinite(openingPercent) || openingPercent < MinimumOpening || openingPercent > MaximumOpening)
        {
            throw new ArgumentOutOfRangeException(nameof(openingPercent), openingPercent, "开度必须是有限值，且在 10 到 100 之间。");
        }
    }
}
