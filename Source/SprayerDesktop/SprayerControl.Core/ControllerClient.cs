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

    public async Task SetNozzleMaskAsync(NozzleMask mask, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] request = ModbusRtuCodec.BuildWriteSingleRegister(slaveAddress, RegisterMap.NozzleMask, mask.Value);
        byte[] response = await channel.TransceiveAsync(request, 8, cancellationToken);
        ModbusRtuCodec.ValidateWriteResponse(response, request);
    }

    public async Task<ControllerCapabilities> ReadCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        byte[] request = ModbusRtuCodec.BuildReadHoldingRegisters(
            slaveAddress, RegisterMap.CapabilityStart, RegisterMap.CapabilityCount);
        byte[] response = await channel.TransceiveAsync(request, 5 + RegisterMap.CapabilityCount * 2, cancellationToken);

        try
        {
            ushort[] values = ModbusRtuCodec.ParseReadHoldingRegisters(
                response, slaveAddress, RegisterMap.CapabilityCount);
            return new ControllerCapabilities(values[0], values[1]);
        }
        catch (ModbusProtocolException exception) when (exception.ExceptionCode == 0x02)
        {
            return ControllerCapabilities.Legacy;
        }
    }

    public async Task WriteOperatingSetpointsAsync(
        ushort duty,
        ushort targetCurrentMicroamps,
        CancellationToken cancellationToken = default)
    {
        ValidateOperatingSetpoints(duty, targetCurrentMicroamps);
        cancellationToken.ThrowIfCancellationRequested();
        ushort[] values = [duty, 0, 0, 0, 0, 30, 80, 0, targetCurrentMicroamps];
        byte[] request = ModbusRtuCodec.BuildWriteMultipleRegisters(slaveAddress, RegisterMap.LinkedDuty, values);
        byte[] response = await channel.TransceiveAsync(request, 8, cancellationToken);
        ModbusRtuCodec.ValidateWriteMultipleResponse(response, request);
    }

    public async Task<ControllerStatus> ReadStatusAsync(CancellationToken cancellationToken = default)
    {
        byte[] request = ModbusRtuCodec.BuildReadHoldingRegisters(slaveAddress, RegisterMap.StatusStart, RegisterMap.StatusCount);
        byte[] response = await channel.TransceiveAsync(request, 5 + RegisterMap.StatusCount * 2, cancellationToken);
        ushort[] value = ModbusRtuCodec.ParseReadHoldingRegisters(response, slaveAddress, RegisterMap.StatusCount);
        return new(value[0], value[1], value[2], value[3], value[4], value[5], value[6], value[7] != 0, value[8], value[9] != 0);
    }

    private static void ValidateOperatingSetpoints(ushort duty, ushort targetCurrentMicroamps)
    {
        if (duty is < 30 or > 80)
            throw new ArgumentOutOfRangeException(nameof(duty), "Duty must be from 30 through 80 percent.");
        if (targetCurrentMicroamps > 20000)
            throw new ArgumentOutOfRangeException(nameof(targetCurrentMicroamps), "Current must be from 0 through 20000 microamps.");
    }
}
