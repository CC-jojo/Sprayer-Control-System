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
        int? lastDutyPercent = null,
        bool powerOnReturnValveEnabled = false,
        int powerOnReturnValvePercent = DefaultPowerOnReturnOpening,
        double? lastReturnOpeningPercent = null)
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
        PowerOnReturnValveEnabled = powerOnReturnValveEnabled;
        PowerOnReturnValvePercent = CoerceOpening(powerOnReturnValvePercent, returnValveSafetyMinimum);
        LastReturnOpeningPercent = CoerceOptionalOpening(lastReturnOpeningPercent, returnValveSafetyMinimum);
        LastModifiedUtc = lastModifiedUtc.ToUniversalTime();
    }

    /* 上电默认回流阀开度：旧配置缺失时为 100%（与固件出厂的安全全开位一致）。 */
    private const int DefaultPowerOnReturnOpening = 100;

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

    /* 开度统一钳制到 [安全下限, 100]：安全下限随后被调高时，旧的上电默认值/记忆值
       自动抬到新下限，避免保存与下发因越界而失败。 */
    private static int CoerceOpening(int openingPercent, int safetyMinimum) =>
        Math.Clamp(openingPercent, safetyMinimum, 100);

    private static double? CoerceOptionalOpening(double? openingPercent, int safetyMinimum)
    {
        if (!openingPercent.HasValue)
            return null;

        if (!double.IsFinite(openingPercent.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(openingPercent),
                openingPercent,
                "记忆回流阀开度必须是有限数值。");
        }

        return Math.Clamp(openingPercent.Value, safetyMinimum, 100);
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

    /* 上电默认回流阀：开关打开时，控制器上电与上位机启动都把回流阀调到该开度；
       关闭时改为继承上次使用的开度（LastReturnOpeningPercent）。 */
    public bool PowerOnReturnValveEnabled { get; }

    public int PowerOnReturnValvePercent { get; }

    /* 上次使用的回流阀开度记忆：程序退出时写入，开关关闭时用于启动恢复。 */
    public double? LastReturnOpeningPercent { get; }

    public DateTimeOffset LastModifiedUtc { get; }
}
