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
    public ControllerParameterLimits ParameterLimits { get; private set; } = ControllerParameterLimits.Default;

    public async Task WriteSettingsAsync(ControllerSettings settings, CancellationToken cancellationToken = default)
    {
        SettingsValidation validation = settings.Validate(
            ParameterLimits.DutyMinimum, ParameterLimits.DutyMaximum, ParameterLimits.CurrentLimitMicroamps);
        if (!validation.IsValid) throw new ArgumentException(validation.ErrorMessage, nameof(settings));

        ushort[] values =
        [
            (ushort)settings.Mode,
            settings.Duty,
            0, 0, 0, 0,
            ParameterLimits.DutyMinimum,
            ParameterLimits.DutyMaximum,
            settings.PersistenceEnabled ? (ushort)1 : (ushort)0,
            settings.TargetCurrentMicroamps
        ];
        byte[] request = ModbusRtuCodec.BuildWriteMultipleRegisters(slaveAddress, RegisterMap.Mode, values);
        byte[] response = await channel.TransceiveAsync(request, 8, cancellationToken);
        ModbusRtuCodec.ValidateWriteMultipleResponse(response, request);
    }

    /* 从固件能力区读取参数限制并缓存。旧固件不支持时回退到安全默认值。 */
    public async Task<ControllerParameterLimits> ReadParameterLimitsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            byte[] request = ModbusRtuCodec.BuildReadHoldingRegisters(
                slaveAddress, RegisterMap.DutyMinCapability, 3);
            byte[] response = await channel.TransceiveAsync(request, 5 + 3 * 2, cancellationToken);
            ushort[] values = ModbusRtuCodec.ParseReadHoldingRegisters(response, slaveAddress, 3);
            ParameterLimits = new ControllerParameterLimits(
                DutyMinimum: values[0],
                DutyMaximum: values[1],
                CurrentLimitMicroamps: values[2]);
        }
        catch (ModbusProtocolException exception) when (exception.ExceptionCode == 0x02)
        {
            ParameterLimits = ControllerParameterLimits.Default;
        }
        return ParameterLimits;
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

        // 注意：日常调整设定值不得触碰 0x0008（PersistenceEnabled），
        // 否则会把用户已保存的“上电记住参数”标志误清为 0。
        // 因此分两段写入：0x0001~0x0007（占空比等）与 0x0009（目标电流），跳过 0x0008。
        // 0x0006/0x0007 写入固件当前限制值，保证运行设定点不覆盖用户配置的 min/max。
        ushort[] dutyValues = [duty, 0, 0, 0, 0, ParameterLimits.DutyMinimum, ParameterLimits.DutyMaximum];
        byte[] dutyRequest = ModbusRtuCodec.BuildWriteMultipleRegisters(
            slaveAddress, RegisterMap.LinkedDuty, dutyValues);
        byte[] dutyResponse = await channel.TransceiveAsync(dutyRequest, 8, cancellationToken);
        ModbusRtuCodec.ValidateWriteMultipleResponse(dutyResponse, dutyRequest);

        ushort[] currentValues = [targetCurrentMicroamps];
        byte[] currentRequest = ModbusRtuCodec.BuildWriteMultipleRegisters(
            slaveAddress, RegisterMap.TargetCurrentMicroamps, currentValues);
        byte[] currentResponse = await channel.TransceiveAsync(currentRequest, 8, cancellationToken);
        ModbusRtuCodec.ValidateWriteMultipleResponse(currentResponse, currentRequest);
    }

    public async Task<ControllerStatus> ReadStatusAsync(CancellationToken cancellationToken = default)
    {
        byte[] request = ModbusRtuCodec.BuildReadHoldingRegisters(slaveAddress, RegisterMap.StatusStart, RegisterMap.StatusCount);
        byte[] response = await channel.TransceiveAsync(request, 5 + RegisterMap.StatusCount * 2, cancellationToken);
        ushort[] value = ModbusRtuCodec.ParseReadHoldingRegisters(response, slaveAddress, RegisterMap.StatusCount);
        return new(value[0], value[1], value[2], value[3], value[4], value[5], value[6], value[7] != 0, value[8], value[9] != 0);
    }

    private void ValidateOperatingSetpoints(ushort duty, ushort targetCurrentMicroamps)
    {
        if (duty < ParameterLimits.DutyMinimum || duty > ParameterLimits.DutyMaximum)
            throw new ArgumentOutOfRangeException(
                nameof(duty),
                $"电磁阀占空比必须在 {ParameterLimits.DutyMinimum}% 到 {ParameterLimits.DutyMaximum}% 之间。");
        if (targetCurrentMicroamps > ParameterLimits.CurrentLimitMicroamps)
            throw new ArgumentOutOfRangeException(
                nameof(targetCurrentMicroamps),
                $"目标电流必须在 0 到 {ParameterLimits.CurrentLimitMicroamps} 微安之间。");
    }
}
