namespace SprayerControl.Core;

public static class RegisterMap
{
    public const ushort Mode = 0x0000;
    public const ushort LinkedDuty = 0x0001;
    public const ushort Group1Duty = 0x0002;
    public const ushort Group2Duty = 0x0003;
    public const ushort Group3Duty = 0x0004;
    public const ushort Group4Duty = 0x0005;
    public const ushort DutyMin = 0x0006;
    public const ushort DutyMax = 0x0007;
    public const ushort PersistenceEnabled = 0x0008;
    public const ushort TargetCurrentMicroamps = 0x0009;
    public const ushort SaveCommand = 0x000A;
    public const ushort PwmEnabled = 0x000B;
    public const ushort NozzleMask = 0x000C;

    public const ushort StatusStart = 0x0100;
    public const ushort StatusCount = 10;
    public const ushort CapabilityStart = 0x010A;
    public const ushort CapabilityCount = 2;
}
