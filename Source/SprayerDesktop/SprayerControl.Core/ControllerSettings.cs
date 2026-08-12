namespace SprayerControl.Core;

public enum SprayMode : ushort { AllOff=0, Group4=1, Groups34=2, Groups234=3, AllGroups=4, Custom=5 }
public readonly record struct SettingsValidation(bool IsValid,string ErrorMessage){public static SettingsValidation Success=>new(true,string.Empty);public static SettingsValidation Failure(string m)=>new(false,m);}
public sealed record ControllerSettings(SprayMode Mode,ushort Duty,bool PersistenceEnabled,ushort TargetCurrentMicroamps)
{
    public static ControllerSettings Default=>new(SprayMode.AllOff,30,false,0);
    public SettingsValidation Validate(){
        if(!Enum.IsDefined(Mode))return SettingsValidation.Failure("喷头工作模式无效。");
        if(Duty is <30 or >80)return SettingsValidation.Failure("统一占空比必须在30%到80%之间。");
        if(TargetCurrentMicroamps>20000)return SettingsValidation.Failure("目标电流必须在0到20.000 mA之间。");
        return SettingsValidation.Success;
    }
}
public sealed record ControllerStatus(ushort PwmTick,ushort CurrentModuleState,ushort TargetCurrentMicroamps,ushort CurrentModuleReadback,ushort CurrentModuleFaultCount,ushort LastParameterError,ushort FirmwareVersion,bool PwmEnabled,ushort OutputMask,bool CommunicationStopped);
