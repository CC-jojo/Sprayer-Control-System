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
}
