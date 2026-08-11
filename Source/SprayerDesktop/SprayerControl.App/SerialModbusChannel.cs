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

                byte[] response = new byte[responseLength];
                int offset = 0;
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
}
