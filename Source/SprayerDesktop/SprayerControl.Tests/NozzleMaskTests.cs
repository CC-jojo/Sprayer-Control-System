using SprayerControl.Core;

namespace SprayerControl.Tests;

public sealed class NozzleMaskTests
{
    [Theory]
    [InlineData(1, false, 0x01)]
    [InlineData(1, true, 0x02)]
    [InlineData(2, false, 0x04)]
    [InlineData(2, true, 0x08)]
    [InlineData(3, false, 0x10)]
    [InlineData(3, true, 0x20)]
    [InlineData(4, false, 0x40)]
    [InlineData(4, true, 0x80)]
    public void WithNozzleEnablesTheMappedIndividualChannel(int group, bool right, byte expected)
    {
        NozzleMask mask = NozzleMask.None.WithNozzle(group, right, enabled: true);

        Assert.Equal(expected, mask.Value);
        Assert.True(mask.IsEnabled(group, right));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    public void WithNozzleDisablesOnlyTheRequestedChannel(int group, bool right)
    {
        NozzleMask mask = NozzleMask.All.WithNozzle(group, right, enabled: false);

        Assert.False(mask.IsEnabled(group, right));
        Assert.Equal((byte)(0xFF ^ (1 << (((group - 1) * 2) + (right ? 1 : 0)))), mask.Value);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(5, false)]
    [InlineData(5, true)]
    public void ChannelOperationsRejectGroupsOutsideOneThroughFour(int group, bool right)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NozzleMask.None.IsEnabled(group, right));
        Assert.Throws<ArgumentOutOfRangeException>(() => NozzleMask.None.WithNozzle(group, right, enabled: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => NozzleMask.None.WithNozzle(group, right, enabled: true));
    }

    [Fact]
    public void SideMastersUseEvenBitsForLeftAndOddBitsForRight()
    {
        Assert.Equal((byte)0x55, NozzleMask.All.WithRight(enabled: false).Value);
        Assert.Equal((byte)0xAA, NozzleMask.All.WithLeft(enabled: false).Value);
    }

    [Fact]
    public void SideMastersPreserveTheOppositeSideAndEnableAllOfTheirOwnChannels()
    {
        NozzleMask asymmetric = new(0x81);

        Assert.Equal((byte)0xD5, asymmetric.WithLeft(enabled: true).Value);
        Assert.Equal((byte)0xAB, asymmetric.WithRight(enabled: true).Value);
        Assert.Equal((byte)0x80, asymmetric.WithLeft(enabled: false).Value);
        Assert.Equal((byte)0x01, asymmetric.WithRight(enabled: false).Value);
    }

    [Theory]
    [InlineData(CanopyPreset.Group4, true, true, 0xC0)]
    [InlineData(CanopyPreset.Group4, true, false, 0x40)]
    [InlineData(CanopyPreset.Group4, false, true, 0x80)]
    [InlineData(CanopyPreset.Group4, false, false, 0x00)]
    [InlineData(CanopyPreset.Groups34, true, true, 0xF0)]
    [InlineData(CanopyPreset.Groups34, true, false, 0x50)]
    [InlineData(CanopyPreset.Groups34, false, true, 0xA0)]
    [InlineData(CanopyPreset.Groups34, false, false, 0x00)]
    [InlineData(CanopyPreset.Groups234, true, true, 0xFC)]
    [InlineData(CanopyPreset.Groups234, true, false, 0x54)]
    [InlineData(CanopyPreset.Groups234, false, true, 0xA8)]
    [InlineData(CanopyPreset.Groups234, false, false, 0x00)]
    [InlineData(CanopyPreset.AllGroups, true, true, 0xFF)]
    [InlineData(CanopyPreset.AllGroups, true, false, 0x55)]
    [InlineData(CanopyPreset.AllGroups, false, true, 0xAA)]
    [InlineData(CanopyPreset.AllGroups, false, false, 0x00)]
    public void PresetCreatesExpectedMaskForEverySideCombination(
        CanopyPreset preset,
        bool leftEnabled,
        bool rightEnabled,
        byte expected)
    {
        Assert.Equal(expected, NozzleMask.ForPreset(preset, leftEnabled, rightEnabled).Value);
    }

    [Fact]
    public void ValueEqualityUsesTheMaskByte()
    {
        Assert.Equal(new NozzleMask(0xA5), new NozzleMask(0xA5));
        Assert.NotEqual(new NozzleMask(0xA5), new NozzleMask(0x5A));
        Assert.Equal(NozzleMask.None, new NozzleMask(0x00));
        Assert.Equal(NozzleMask.All, new NozzleMask(0xFF));
    }

    [Fact]
    public void ForPresetRejectsUnknownEnumValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NozzleMask.ForPreset((CanopyPreset)99, leftEnabled: true, rightEnabled: true));
    }

    [Theory]
    [InlineData(0x00, 0x00)] // 全关 → 全关
    [InlineData(0x01, 0x40)] // 界面 左一 → 物理 左四(bit6)
    [InlineData(0x02, 0x08)] // 界面 右一 → 物理 右二(bit3)
    [InlineData(0x04, 0x01)] // 界面 左二 → 物理 左一(bit0)
    [InlineData(0x08, 0x80)] // 界面 右二 → 物理 右四(bit7)
    [InlineData(0x10, 0x04)] // 界面 左三 → 物理 左二(bit2)
    [InlineData(0x20, 0x02)] // 界面 右三 → 物理 右一(bit1)
    [InlineData(0x40, 0x10)] // 界面 左四 → 物理 左三(bit4)
    [InlineData(0x80, 0x20)] // 界面 右四 → 物理 右三(bit5)
    [InlineData(0xA5, 0x63)]
    [InlineData(0xFF, 0xFF)]
    public void UiMaskMapsToPhysicalAndBack(int uiValue, byte physicalExpected)
    {
        NozzleMask ui = new((byte)uiValue);

        NozzleMask physical = ui.ToPhysical();

        Assert.Equal(physicalExpected, physical.Value);
        Assert.Equal(ui, physical.ToUi());
    }

    [Fact]
    public void PhysicalMappingMatchesTheFieldWiringTable()
    {
        Assert.Equal((byte)0x40, NozzleMask.None.WithNozzle(1, right: false, enabled: true).ToPhysical().Value); // 左一 → 左四
        Assert.Equal((byte)0x01, NozzleMask.None.WithNozzle(2, right: false, enabled: true).ToPhysical().Value); // 左二 → 左一
        Assert.Equal((byte)0x04, NozzleMask.None.WithNozzle(3, right: false, enabled: true).ToPhysical().Value); // 左三 → 左二
        Assert.Equal((byte)0x10, NozzleMask.None.WithNozzle(4, right: false, enabled: true).ToPhysical().Value); // 左四 → 左三
        Assert.Equal((byte)0x20, NozzleMask.None.WithNozzle(4, right: true, enabled: true).ToPhysical().Value);  // 右四 → 右三
        Assert.Equal((byte)0x02, NozzleMask.None.WithNozzle(3, right: true, enabled: true).ToPhysical().Value);  // 右三 → 右一
        Assert.Equal((byte)0x80, NozzleMask.None.WithNozzle(2, right: true, enabled: true).ToPhysical().Value);  // 右二 → 右四
        Assert.Equal((byte)0x08, NozzleMask.None.WithNozzle(1, right: true, enabled: true).ToPhysical().Value);  // 右一 → 右二
    }

    [Fact]
    public void CustomMappingDrivesBothPhysicalAndUiTransforms()
    {
        byte[] identity = [0, 1, 2, 3, 4, 5, 6, 7];

        Assert.Equal((byte)0x05, new NozzleMask(0x05).ToPhysical(identity).Value);
        Assert.Equal((byte)0x05, new NozzleMask(0x05).ToUi(NozzleMask.BuildInverseMap(identity)).Value);
    }

    [Fact]
    public void MappingValidationRejectsInvalidTables()
    {
        Assert.Throws<ArgumentNullException>(() => NozzleMask.ValidateMapping(null!));
        Assert.Throws<ArgumentException>(() => NozzleMask.ValidateMapping([]));
        Assert.Throws<ArgumentException>(() => NozzleMask.ValidateMapping([0, 1, 2, 3, 4, 5, 6]));
        Assert.Throws<ArgumentOutOfRangeException>(() => NozzleMask.ValidateMapping([0, 1, 2, 3, 4, 5, 6, 8]));
        Assert.Throws<ArgumentException>(() => NozzleMask.ValidateMapping([0, 1, 2, 3, 4, 5, 6, 6]));

        byte[] valid = [0, 1, 2, 3, 4, 5, 6, 7];
        Assert.NotSame(valid, NozzleMask.ValidateMapping(valid));
        Assert.Equal(valid, NozzleMask.ValidateMapping(valid));
    }
}
