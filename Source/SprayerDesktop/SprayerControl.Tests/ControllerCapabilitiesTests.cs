using SprayerControl.Core;

namespace SprayerControl.Tests;

public sealed class ControllerCapabilitiesTests
{
    [Fact]
    public void ExplicitMaskSupportUsesOnlyTheDefinedFeatureBit()
    {
        Assert.False(new ControllerCapabilities(2, 0x8000).SupportsExplicitNozzleMask);
        Assert.True(new ControllerCapabilities(2, 0x8001).SupportsExplicitNozzleMask);
        Assert.Equal((ushort)0x8001, new ControllerCapabilities(2, 0x8001).FeatureFlags);
    }

    [Fact]
    public void LegacyCapabilityIsProtocolOneWithoutFeatures()
    {
        Assert.Equal(new ControllerCapabilities(1, 0), ControllerCapabilities.Legacy);
    }
}
