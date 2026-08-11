namespace SprayerControl.Core;

public sealed class HostConfiguration
{
    public HostConfiguration(
        int schemaVersion,
        int pwmDutyMinimum,
        int returnValveSafetyMinimum,
        ReturnValveCalibration returnValveCalibration,
        DateTimeOffset lastModifiedUtc)
    {
        if (schemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Schema version must be at least 1.");
        }

        if (pwmDutyMinimum is < 30 or > 80)
        {
            throw new ArgumentOutOfRangeException(nameof(pwmDutyMinimum), pwmDutyMinimum, "PWM duty minimum must be from 30 through 80.");
        }

        if (returnValveSafetyMinimum is < 10 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(returnValveSafetyMinimum), returnValveSafetyMinimum, "Return-valve safety minimum must be from 10 through 100.");
        }

        ArgumentNullException.ThrowIfNull(returnValveCalibration);
        if (lastModifiedUtc == default)
        {
            throw new ArgumentException("Last modified timestamp must be set.", nameof(lastModifiedUtc));
        }

        SchemaVersion = schemaVersion;
        PwmDutyMinimum = pwmDutyMinimum;
        ReturnValveSafetyMinimum = returnValveSafetyMinimum;
        ReturnValveCalibration = new ReturnValveCalibration(returnValveCalibration.Points);
        LastModifiedUtc = lastModifiedUtc.ToUniversalTime();
    }

    public int SchemaVersion { get; }

    public int PwmDutyMinimum { get; }

    public int ReturnValveSafetyMinimum { get; }

    public ReturnValveCalibration ReturnValveCalibration { get; }

    public DateTimeOffset LastModifiedUtc { get; }
}
