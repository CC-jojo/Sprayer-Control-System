using System.IO.Ports;
using SprayerControl.Core;

namespace SprayerControl.App;

public sealed class SerialModbusChannel : IModbusChannel
{
    private readonly SerialPort _port;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public SerialModbusChannel(string portName, int baudRate = SerialCommunicationDefaults.BaudRate)
    {
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 700,
            WriteTimeout = 500
        };
        _port.Open();
    }

    public async Task<byte[]> TransceiveAsync(byte[] request, int responseLength, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                _port.DiscardInBuffer();
                _port.Write(request, 0, request.Length);

                if (responseLength < 5)
                    throw new ArgumentOutOfRangeException(nameof(responseLength));

                byte[] header = new byte[2];
                ReadExactly(header, 0, header.Length, cancellationToken);
                int resolvedLength = ModbusRtuCodec.DetermineResponseLength(header, responseLength);
                byte[] response = new byte[resolvedLength];
                header.CopyTo(response, 0);
                int offset = header.Length;
                while (offset < response.Length)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    offset += _port.Read(response, offset, response.Length - offset);
                }
                return response;
            }, cancellationToken);
        }
        catch (System.TimeoutException)
        {
            throw new TimeoutException("控制器在 700 ms 内未回传确认。");
        }
        finally
        {
            _mutex.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _port.Dispose();
        _mutex.Dispose();
        return ValueTask.CompletedTask;
    }

    private void ReadExactly(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int read = 0;
        while (read < count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            read += _port.Read(buffer, offset + read, count - read);
        }
    }
}
