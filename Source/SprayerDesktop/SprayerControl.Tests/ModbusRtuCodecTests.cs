using SprayerControl.Core;

namespace SprayerControl.Tests;

public sealed class ModbusRtuCodecTests
{
    [Fact]
    public void CalculatesStandardModbusCrc()
    {
        byte[] payload = [0x01, 0x06, 0x00, 0x32, 0x00, 0x02];

        Assert.Equal(0xC4A9, ModbusRtuCodec.CalculateCrc(payload));
    }

    [Fact]
    public void BuildsReadHoldingRegistersRequest()
    {
        byte[] frame = ModbusRtuCodec.BuildReadHoldingRegisters(1, 0x0100, 7);

        Assert.Equal([0x01, 0x03, 0x01, 0x00, 0x00, 0x07, 0x05, 0xF4], frame);
    }

    [Fact]
    public void BuildsWriteMultipleRegistersRequest()
    {
        byte[] frame = ModbusRtuCodec.BuildWriteMultipleRegisters(1, 0, [0, 10, 10, 10]);

        Assert.True(ModbusRtuCodec.HasValidCrc(frame));
        Assert.Equal([0x01, 0x10, 0x00, 0x00, 0x00, 0x04, 0x08], frame[..7]);
        Assert.Equal([0x00, 0x00, 0x00, 0x0A, 0x00, 0x0A, 0x00, 0x0A], frame[7..^2]);
    }

    [Fact]
    public void ParsesReadHoldingRegistersResponse()
    {
        byte[] response = ModbusRtuCodec.WithCrc([0x01, 0x03, 0x04, 0x00, 0x0A, 0x00, 0x14]);

        ushort[] values = ModbusRtuCodec.ParseReadHoldingRegisters(response, 1, 2);

        Assert.Equal([10, 20], values);
    }

    [Fact]
    public void RejectsCorruptedResponse()
    {
        byte[] response = ModbusRtuCodec.WithCrc([0x01, 0x03, 0x02, 0x00, 0x0A]);
        response[^1] ^= 0xFF;

        Assert.Throws<ModbusProtocolException>(() =>
            ModbusRtuCodec.ParseReadHoldingRegisters(response, 1, 1));
    }
}
