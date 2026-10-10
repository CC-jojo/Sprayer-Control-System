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

    /* 界面位（UI）→ 物理喷头位（固件）默认映射：按现场喷头接线映射表确定。
       索引 = 界面位（bit0=左一、bit1=右一…bit7=右四），值 = 物理喷头位。
       该表可在设置页自定义并本地保存，会话层通过 UpdateNozzleMapping 使用。 */
    public static readonly byte[] DefaultUiToPhysicalBit = [6, 3, 0, 7, 2, 1, 4, 5];

    public static byte[] BuildInverseMap(byte[] uiToPhysicalBit)
    {
        ArgumentNullException.ThrowIfNull(uiToPhysicalBit);
        byte[] inverse = new byte[8];
        for (int uiBit = 0; uiBit < 8; uiBit++)
            inverse[uiToPhysicalBit[uiBit]] = (byte)uiBit;
        return inverse;
    }

    public static byte[] ValidateMapping(byte[] uiToPhysicalBit)
    {
        ArgumentNullException.ThrowIfNull(uiToPhysicalBit);
        if (uiToPhysicalBit.Length != 8)
            throw new ArgumentException("电磁阀映射必须包含 8 个物理喷头。", nameof(uiToPhysicalBit));

        Span<bool> seen = stackalloc bool[8];
        foreach (byte value in uiToPhysicalBit)
        {
            if (value > 7)
                throw new ArgumentOutOfRangeException(nameof(uiToPhysicalBit), "物理喷头编号必须在 0 到 7 之间。");
            if (seen[value])
                throw new ArgumentException("物理喷头编号不能重复。", nameof(uiToPhysicalBit));
            seen[value] = true;
        }

        return (byte[])uiToPhysicalBit.Clone();
    }

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
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "未知的冠层预设。"),
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

    /// <summary>按默认映射把界面位掩码转换为物理喷头位掩码，仅在向固件下发时使用。</summary>
    public NozzleMask ToPhysical() => ToPhysical(DefaultUiToPhysicalBit);

    /// <summary>按默认映射把固件读回的物理喷头位掩码还原为界面位掩码，仅用于界面显示。</summary>
    public NozzleMask ToUi() => ToUi(BuildInverseMap(DefaultUiToPhysicalBit));

    /// <summary>把界面位掩码映射为物理喷头位掩码（自定义映射表），仅在向固件下发时使用。</summary>
    public NozzleMask ToPhysical(byte[] uiToPhysicalBit)
    {
        byte result = 0;
        for (int uiBit = 0; uiBit < 8; uiBit++)
        {
            if ((Value & (1 << uiBit)) != 0)
                result |= (byte)(1 << uiToPhysicalBit[uiBit]);
        }
        return new NozzleMask(result);
    }

    /// <summary>把固件读回的物理喷头位掩码还原为界面位掩码（自定义映射表），仅用于界面显示。</summary>
    public NozzleMask ToUi(byte[] physicalToUiBit)
    {
        byte result = 0;
        for (int physicalBit = 0; physicalBit < 8; physicalBit++)
        {
            if ((Value & (1 << physicalBit)) != 0)
                result |= (byte)(1 << physicalToUiBit[physicalBit]);
        }
        return new NozzleMask(result);
    }

    private static byte BitFor(int group, bool right)
    {
        if (group is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(group), group, "喷头组号从 1 开始，必须在 1 到 4 之间。");
        }

        int bit = ((group - 1) * 2) + (right ? 1 : 0);
        return (byte)(1 << bit);
    }
}
