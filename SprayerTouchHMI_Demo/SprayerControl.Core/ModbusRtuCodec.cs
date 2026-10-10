namespace SprayerControl.Core;

public sealed class ModbusProtocolException : Exception
{
    public ModbusProtocolException(string message, byte? exceptionCode = null) : base(message)
    {
        ExceptionCode = exceptionCode;
    }

    public byte? ExceptionCode { get; }
}

public static class ModbusRtuCodec
{
    public static ushort CalculateCrc(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (byte value in data)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
                crc = (ushort)((crc & 1) != 0 ? (crc >> 1) ^ 0xA001 : crc >> 1);
        }
        return crc;
    }

    public static bool HasValidCrc(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 4) return false;
        ushort expected = CalculateCrc(frame[..^2]);
        return frame[^2] == (byte)expected && frame[^1] == (byte)(expected >> 8);
    }

    public static byte[] WithCrc(ReadOnlySpan<byte> payload)
    {
        byte[] frame = new byte[payload.Length + 2];
        payload.CopyTo(frame);
        ushort crc = CalculateCrc(payload);
        frame[^2] = (byte)crc;
        frame[^1] = (byte)(crc >> 8);
        return frame;
    }

    public static byte[] BuildReadHoldingRegisters(byte slave, ushort start, ushort count) =>
        WithCrc([slave, 0x03, (byte)(start >> 8), (byte)start, (byte)(count >> 8), (byte)count]);

    public static byte[] BuildWriteSingleRegister(byte slave, ushort address, ushort value) =>
        WithCrc([slave, 0x06, (byte)(address >> 8), (byte)address, (byte)(value >> 8), (byte)value]);

    public static byte[] BuildWriteMultipleRegisters(byte slave, ushort start, ReadOnlySpan<ushort> values)
    {
        if (values.Length is < 1 or > 123)
            throw new ArgumentOutOfRangeException(nameof(values));
        byte[] payload = new byte[7 + values.Length * 2];
        payload[0] = slave;
        payload[1] = 0x10;
        payload[2] = (byte)(start >> 8);
        payload[3] = (byte)start;
        payload[4] = (byte)(values.Length >> 8);
        payload[5] = (byte)values.Length;
        payload[6] = (byte)(values.Length * 2);
        for (int index = 0; index < values.Length; index++)
        {
            payload[7 + index * 2] = (byte)(values[index] >> 8);
            payload[8 + index * 2] = (byte)values[index];
        }
        return WithCrc(payload);
    }

    public static int DetermineResponseLength(ReadOnlySpan<byte> header, int requestedResponseLength)
    {
        if (header.Length < 2)
            throw new ArgumentException("Modbus 响应头必须包含地址和功能码字节。", nameof(header));
        if (requestedResponseLength < 5)
            throw new ArgumentOutOfRangeException(nameof(requestedResponseLength));

        return (header[1] & 0x80) != 0 ? 5 : requestedResponseLength;
    }

    public static ushort[] ParseReadHoldingRegisters(ReadOnlySpan<byte> frame, byte slave, ushort count)
    {
        Validate(frame, slave, 0x03);
        if (frame.Length != 5 + count * 2 || frame[2] != count * 2)
            throw new ModbusProtocolException("读响应长度无效。");
        ushort[] values = new ushort[count];
        for (int index = 0; index < count; index++)
            values[index] = (ushort)((frame[3 + index * 2] << 8) | frame[4 + index * 2]);
        return values;
    }

    public static void ValidateWriteResponse(ReadOnlySpan<byte> response, ReadOnlySpan<byte> request)
    {
        Validate(response, request[0], request[1]);
        if (!response.SequenceEqual(request))
            throw new ModbusProtocolException("写响应未回显请求。");
    }

    public static void ValidateWriteMultipleResponse(ReadOnlySpan<byte> response, ReadOnlySpan<byte> request)
    {
        Validate(response, request[0], 0x10);
        if (response.Length != 8 || !response[2..6].SequenceEqual(request[2..6]))
            throw new ModbusProtocolException("批量写响应与请求不匹配。");
    }

    private static void Validate(ReadOnlySpan<byte> frame, byte slave, byte function)
    {
        if (!HasValidCrc(frame)) throw new ModbusProtocolException("Modbus CRC 校验失败。");
        if (frame[0] != slave) throw new ModbusProtocolException("从站地址不匹配。");
        if (frame[1] == (function | 0x80))
        {
            if (frame.Length != 5)
                throw new ModbusProtocolException("Modbus 异常响应长度无效。");
            throw new ModbusProtocolException($"从站返回 Modbus 异常 0x{frame[2]:X2}。", frame[2]);
        }
        if (frame[1] != function) throw new ModbusProtocolException("功能码不匹配。");
    }
}
