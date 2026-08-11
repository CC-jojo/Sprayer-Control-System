namespace SprayerControl.Core;

public enum CanopyPreset
{
    Group4,
    Groups34,
    Groups234,
    AllGroups,
}

public readonly record struct NozzleMask(byte Value)
{
    private const byte LeftBits = 0x55;
    private const byte RightBits = 0xAA;

    public static NozzleMask None => new(0x00);

    public static NozzleMask All => new(0xFF);

    public bool IsEnabled(int group, bool right) => (Value & BitFor(group, right)) != 0;

    public NozzleMask WithNozzle(int group, bool right, bool enabled)
    {
        byte bit = BitFor(group, right);
        return new NozzleMask(enabled ? (byte)(Value | bit) : (byte)(Value & ~bit));
    }

    public NozzleMask WithLeft(bool enabled) => new(enabled ? (byte)(Value | LeftBits) : (byte)(Value & ~LeftBits));

    public NozzleMask WithRight(bool enabled) => new(enabled ? (byte)(Value | RightBits) : (byte)(Value & ~RightBits));

    public static NozzleMask ForPreset(CanopyPreset preset, bool leftEnabled, bool rightEnabled)
    {
        NozzleMask mask = preset switch
        {
            CanopyPreset.Group4 => new NozzleMask(0xC0),
            CanopyPreset.Groups34 => new NozzleMask(0xF0),
            CanopyPreset.Groups234 => new NozzleMask(0xFC),
            CanopyPreset.AllGroups => All,
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown canopy preset."),
        };

        if (!leftEnabled)
        {
            mask = mask.WithLeft(enabled: false);
        }

        if (!rightEnabled)
        {
            mask = mask.WithRight(enabled: false);
        }

        return mask;
    }

    private static byte BitFor(int group, bool right)
    {
        if (group is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(group), group, "Groups are one-based and must be from 1 through 4.");
        }

        int bit = ((group - 1) * 2) + (right ? 1 : 0);
        return (byte)(1 << bit);
    }
}
