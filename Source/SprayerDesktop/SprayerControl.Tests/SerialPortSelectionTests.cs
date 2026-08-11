using SprayerControl.Core;

namespace SprayerControl.Tests;

public sealed class SerialPortSelectionTests
{
    [Fact]
    public void UsesRobustFieldBaudRate()
    {
        Assert.Equal(38400, SerialCommunicationDefaults.BaudRate);
    }

    [Fact]
    public void PrefersCh343OverStLinkAndBluetoothPorts()
    {
        SerialPortDescriptor[] ports =
        [
            new("COM12", "STMicroelectronics STLink Virtual COM Port"),
            new("COM3", "蓝牙链接上的标准串行"),
            new("COM9", "USB-Enhanced-SERIAL CH343")
        ];

        Assert.Equal("COM9", SerialPortSelection.ChoosePreferred(ports)?.PortName);
    }

    [Fact]
    public void RetainsPreviousPortWhenItIsStillPresent()
    {
        SerialPortDescriptor[] ports =
        [
            new("COM9", "USB-Enhanced-SERIAL CH343"),
            new("COM10", "USB Serial Device")
        ];

        Assert.Equal("COM10", SerialPortSelection.ChoosePreferred(ports, "COM10")?.PortName);
    }
}
