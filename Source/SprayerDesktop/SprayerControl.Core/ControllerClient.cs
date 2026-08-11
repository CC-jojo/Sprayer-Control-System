namespace SprayerControl.Core;

public interface IModbusChannel : IAsyncDisposable
{
    Task<byte[]> TransceiveAsync(
        byte[] request,
        int responseLength,
        CancellationToken cancellationToken = default);
}

public sealed class ControllerClient(IModbusChannel channel, byte slaveAddress = 1)
{
    public async Task WriteSettingsAsync(ControllerSettings settings, CancellationToken cancellationToken = default)
    {
        SettingsValidation validation = settings.Validate();
        if (!validation.IsValid) throw new ArgumentException(validation.ErrorMessage, nameof(settings));

        ushort[] values =
        [
            (ushort)settings.Mode,
            settings.Duty,
            0, 0, 0, 0,
            30,
            80,
            settings.PersistenceEnabled ? (ushort)1 : (ushort)0,
            settings.TargetCurrentMicroamps
        ];
        byte[] request = ModbusRtuCodec.BuildWriteMultipleRegisters(slaveAddress, RegisterMap.Mode, values);
        byte[] response = await channel.TransceiveAsync(request, 8, cancellationToken);
        ModbusRtuCodec.ValidateWriteMultipleResponse(response, request);
    }

    public async Task SaveToFlashAsync(CancellationToken cancellationToken = default)
    {
        byte[] request = ModbusRtuCodec.BuildWriteSingleRegister(slaveAddress, RegisterMap.SaveCommand, 0xA55A);
        byte[] response = await channel.TransceiveAsync(request, 8, cancellationToken);
        ModbusRtuCodec.ValidateWriteResponse(response, request);
    }

    public async Task SetPwmEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        byte[] request = ModbusRtuCodec.BuildWriteSingleRegister(
            slaveAddress, RegisterMap.PwmEnabled, enabled ? (ushort)1 : (ushort)0);
        byte[] response = await channel.TransceiveAsync(request, 8, cancellationToken);
        ModbusRtuCodec.ValidateWriteResponse(response, request);
    }

    public async Task<ControllerStatus> ReadStatusAsync(CancellationToken cancellationToken = default)
    {
        byte[] request = ModbusRtuCodec.BuildReadHoldingRegisters(slaveAddress, RegisterMap.StatusStart, RegisterMap.StatusCount);
        byte[] response = await channel.TransceiveAsync(request, 5 + RegisterMap.StatusCount * 2, cancellationToken);
        ushort[] value = ModbusRtuCodec.ParseReadHoldingRegisters(response, slaveAddress, RegisterMap.StatusCount);
        return new(value[0], value[1], value[2], value[3], value[4], value[5], value[6], value[7] != 0, value[8], value[9] != 0);
    }
}
