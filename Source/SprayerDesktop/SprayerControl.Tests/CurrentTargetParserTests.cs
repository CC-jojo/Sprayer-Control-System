using SprayerControl.Core;

namespace SprayerControl.Tests;

public sealed class CurrentTargetParserTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("8.5", 8500)]
    [InlineData("10.000", 10000)]
    [InlineData("20", 20000)]
    public void ParsesMilliampsAsIntegerMicroamps(string text, ushort expected)
    {
        Assert.Equal(expected, CurrentTargetParser.ParseMilliamps(text));
    }

    [Theory]
    [InlineData("-0.001")]
    [InlineData("20.001")]
    [InlineData("1.0001")]
    [InlineData("abc")]
    public void RejectsInvalidCurrentTargets(string text)
    {
        Assert.Throws<ArgumentException>(() => CurrentTargetParser.ParseMilliamps(text));
    }
}
