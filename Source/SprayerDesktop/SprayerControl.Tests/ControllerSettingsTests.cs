using SprayerControl.Core;
namespace SprayerControl.Tests;
public sealed class ControllerSettingsTests
{
 [Fact] public void DefaultsAreSafe(){Assert.Equal(SprayMode.AllOff,ControllerSettings.Default.Mode);Assert.Equal((ushort)30,ControllerSettings.Default.Duty);Assert.True(ControllerSettings.Default.Validate().IsValid);}
 [Theory][InlineData(29)][InlineData(81)]public void DutyOutsideRangeIsRejected(ushort duty)=>Assert.False((ControllerSettings.Default with{Duty=duty}).Validate().IsValid);
 [Fact]public void TargetCurrentOutsideRangeIsRejected()=>Assert.False((ControllerSettings.Default with{TargetCurrentMicroamps=20001}).Validate().IsValid);
}
