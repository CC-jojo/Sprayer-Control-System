namespace SprayerControl.Core;

public sealed record ControllerCapabilities(ushort ProtocolVersion, ushort FeatureFlags)
{
    public const ushort ExplicitNozzleMaskFeature = 0x0001;

    public static ControllerCapabilities Legacy { get; } = new(1, 0);

    public bool SupportsExplicitNozzleMask =>
        (FeatureFlags & ExplicitNozzleMaskFeature) != 0;
}
