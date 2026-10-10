namespace SprayerControl.Core;

public sealed class HostConfiguration
{
    public HostConfiguration(
        int schemaVersion,
        int pwmDutyMinimum,
        int returnValveSafetyMinimum,
        ReturnValveCalibration returnValveCalibration,
        DateTimeOffset lastModifiedUtc,
        byte[]? nozzleMapping = null,
        int? lastDutyPercent = null)
    {
        if (schemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "配置结构版本至少为 1。");
        }

        /* 本地配置下限允许到 100，与固件允许的占空比范围一致；
           实际写入固件时仍受固件能力区上报的 duty_min/max 校验约束。 */
        if (pwmDutyMinimum is < 30 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pwmDutyMinimum), pwmDutyMinimum, "电磁阀占空比下限必须在 30% 到 100% 之间。");
        }

        if (returnValveSafetyMinimum is < 10 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(returnValveSafetyMinimum), returnValveSafetyMinimum, "回流阀安全下限必须在 10% 到 100% 之间。");
        }

        ArgumentNullException.ThrowIfNull(returnValveCalibration);
        if (lastModifiedUtc == default)
        {
            throw new ArgumentException("必须设置配置的最后修改时间。", nameof(lastModifiedUtc));
        }

        SchemaVersion = schemaVersion;
        PwmDutyMinimum = pwmDutyMinimum;
        ReturnValveSafetyMinimum = returnValveSafetyMinimum;
        ReturnValveCalibration = new ReturnValveCalibration(returnValveCalibration.Points);
        NozzleMapping = ValidateNozzleMapping(nozzleMapping);
        LastDutyPercent = ValidateLastDutyPercent(lastDutyPercent);
        LastModifiedUtc = lastModifiedUtc.ToUniversalTime();
    }

    /* 上次使用的电磁阀占空比记忆：程序退出时写入，下次启动恢复并下发。 */
    private static int? ValidateLastDutyPercent(int? lastDutyPercent)
    {
        if (lastDutyPercent.HasValue && lastDutyPercent.Value is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastDutyPercent),
                lastDutyPercent,
                "记忆占空比必须在 1% 到 100% 之间。");
        }

        return lastDutyPercent;
    }

    /* 电磁阀八路映射：界面开关位（bit0=左一…bit7=右四）→ 物理喷头位。
       null 时使用默认现场接线映射；否则必须为 8 个互不重复的 0..7。 */
    private static byte[] ValidateNozzleMapping(byte[]? nozzleMapping)
    {
        if (nozzleMapping is null)
            return (byte[])NozzleMask.DefaultUiToPhysicalBit.Clone();
        return NozzleMask.ValidateMapping(nozzleMapping);
    }

    public int SchemaVersion { get; }

    public int PwmDutyMinimum { get; }

    public int ReturnValveSafetyMinimum { get; }

    public ReturnValveCalibration ReturnValveCalibration { get; }

    public byte[] NozzleMapping { get; }

    public int? LastDutyPercent { get; }

    public DateTimeOffset LastModifiedUtc { get; }
}
