namespace SprayerControl.Core;

public enum SprayMode : ushort { AllOff=0, Group4=1, Groups34=2, Groups234=3, AllGroups=4, Custom=5 }
public readonly record struct SettingsValidation(bool IsValid,string ErrorMessage){public static SettingsValidation Success=>new(true,string.Empty);public static SettingsValidation Failure(string m)=>new(false,m);}

/* 固件通过能力区上报的运行时参数限制；上位机连接后读取并据此校验。 */
public sealed record ControllerParameterLimits(ushort DutyMinimum, ushort DutyMaximum, ushort CurrentLimitMicroamps)
{
    public static ControllerParameterLimits Default { get; } = new(30, 80, 20000);
}

public sealed record ControllerSettings(SprayMode Mode,ushort Duty,bool PersistenceEnabled,ushort TargetCurrentMicroamps)
{
    public static ControllerSettings Default=>new(SprayMode.AllOff,30,false,0);
    public SettingsValidation Validate(
        ushort dutyMinimum = 30,
        ushort dutyMaximum = 80,
        ushort currentLimitMicroamps = 20000)
    {
        if(!Enum.IsDefined(Mode))return SettingsValidation.Failure("喷头工作模式无效。");
        if(Duty < dutyMinimum || Duty > dutyMaximum)return SettingsValidation.Failure($"统一占空比必须在{dutyMinimum}%到{dutyMaximum}%之间。");
        if(TargetCurrentMicroamps>currentLimitMicroamps)return SettingsValidation.Failure($"目标电流必须在0到{currentLimitMicroamps / 1000.0:F3} mA之间。");
        return SettingsValidation.Success;
    }
}
public sealed record ControllerStatus(ushort PwmTick,ushort CurrentModuleState,ushort TargetCurrentMicroamps,ushort CurrentModuleReadback,ushort CurrentModuleFaultCount,ushort LastParameterError,ushort FirmwareVersion,bool PwmEnabled,ushort OutputMask,bool CommunicationStopped);
