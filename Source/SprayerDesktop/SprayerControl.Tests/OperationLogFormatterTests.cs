using SprayerControl.Core;

namespace SprayerControl.Tests;

public sealed class OperationLogFormatterTests
{
    [Fact]
    public void FormatsChineseMessageWithMillisecondTimestamp()
    {
        DateTime time = new(2026, 7, 16, 14, 32, 8, 125);

        Assert.Equal("[14:32:08.125] PWM 总开关已开启",
            OperationLogFormatter.Format(time, "PWM 总开关已开启"));
    }
}
