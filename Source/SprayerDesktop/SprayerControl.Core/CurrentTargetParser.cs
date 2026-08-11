using System.Globalization;

namespace SprayerControl.Core;

public static class CurrentTargetParser
{
    public static ushort ParseMilliamps(string text)
    {
        if (!decimal.TryParse(
                text,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.InvariantCulture,
                out decimal milliamps))
        {
            throw new ArgumentException("目标电流必须是0到20.000之间的数字。");
        }

        decimal microamps = milliamps * 1000m;
        if (milliamps < 0m || milliamps > 20m || decimal.Truncate(microamps) != microamps)
        {
            throw new ArgumentException("目标电流必须在0到20.000 mA之间，最多输入三位小数。");
        }

        return decimal.ToUInt16(microamps);
    }
}
