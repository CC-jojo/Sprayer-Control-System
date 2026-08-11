using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SprayerControl.Core;

namespace SprayerControl.App;
public partial class MainWindow:Window
{
    private ControllerConnection? _connection; private ControllerClient? _client; private readonly CancellationTokenSource _lifetime=new();
    private Task? _discoveryTask; private readonly ObservableCollection<string> _log=[]; private readonly DispatcherTimer _heartbeat;
    private bool _heartbeatBusy,_syncSwitch,_confirmedEnabled; private int _ok,_fail;
    public MainWindow(){InitializeComponent();OperationLogList.ItemsSource=_log;AddLog("软件启动，自动扫描控制器；连接后同步控制器实际状态");Loaded+=(_,_)=>StartDiscovery();
        _heartbeat=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};_heartbeat.Tick+=Heartbeat_Tick;_heartbeat.Start();}
    private void StartDiscovery(){if(!_lifetime.IsCancellationRequested&&(_discoveryTask is null||_discoveryTask.IsCompleted))_discoveryTask=DiscoverAsync(_lifetime.Token);}
    private async Task DiscoverAsync(CancellationToken token){SetEnabled(false);ConnectionText.Text="正在自动查找控制器……";try{while(!token.IsCancellationRequested&&_connection is null){SerialPortDescriptor[] ports=await WindowsSerialPortDiscovery.GetPortsAsync(token);AddLog($"发现 {ports.Length} 个串口，开始握手");var found=await Task.Run(()=>ControllerDiscovery.TryDiscoverAsync(ports,p=>new ReliableModbusChannel(new SerialModbusChannel(p.PortName)),token,ReportProbe),token);if(found is not null){_connection=found;_client=found.Client;ApplyStatus(found.Status);ConnectionText.Text="控制器已连接";SetEnabled(true);AddLog("已识别并连接控制器");return;}await Task.Delay(2000,token);}}catch(OperationCanceledException)when(token.IsCancellationRequested){}}
    private void ReportProbe(ControllerProbeResult result)=>Dispatcher.Invoke(()=>AddLog(result.Outcome switch{ControllerProbeOutcome.Connected=>$"{result.Port.PortName} 握手成功（{result.Detail}）",ControllerProbeOutcome.UnsupportedFirmware=>$"{result.Port.PortName} 固件不受支持（{result.Detail}）",_=>$"{result.Port.PortName} 握手失败：{result.Detail}"}));
    private void SetEnabled(bool e){PwmGroup.IsEnabled=e;ReturnValveGroup.IsEnabled=e;CommandButtons.IsEnabled=e;}
    private async Task DisconnectAsync(){var old=_connection;_connection=null;_client=null;SetEnabled(false);ConnectionText.Text="通信中断，控制器独立运行，正在重新连接……";AddLog("通信中断，控制器继续按当前参数独立运行，重新扫描");if(old is not null)await old.DisposeAsync();StartDiscovery();}
    private ControllerClient Client=>_client??throw new InvalidOperationException("控制器尚未连接。");
    private SprayMode SelectedMode()=>Mode4.IsChecked==true?SprayMode.Group4:Mode34.IsChecked==true?SprayMode.Groups34:Mode234.IsChecked==true?SprayMode.Groups234:ModeAll.IsChecked==true?SprayMode.AllGroups:SprayMode.AllOff;
    private static ushort Parse(System.Windows.Controls.TextBox box,string name)=>ushort.TryParse(box.Text,out var v)?v:throw new ArgumentException($"{name}必须是整数。");
    private ControllerSettings ReadSettings()=>new(SelectedMode(),Parse(DutyText,"统一占空比"),false,CurrentTargetParser.ParseMilliamps(TargetCurrent.Text));
    private async void Heartbeat_Tick(object? s,EventArgs e){if(_heartbeatBusy||_client is null)return;_heartbeatBusy=true;try{ApplyStatus(await Client.ReadStatusAsync(_lifetime.Token));}catch(Exception ex)when(ex is TimeoutException or IOException or ModbusProtocolException){await DisconnectAsync();}finally{_heartbeatBusy=false;}}
    private async void PwmMasterSwitch_Click(object s,RoutedEventArgs e){if(_syncSwitch)return;bool requested=PwmMasterSwitch.IsChecked==true;try{await ExecuteAsync(requested?"开启PWM总开关":"关闭PWM总开关",()=>Client.SetPwmEnabledAsync(requested));_confirmedEnabled=requested;}catch(Exception ex){SetSwitch(_confirmedEnabled);ShowError(ex);}}
    private async void Apply_Click(object s,RoutedEventArgs e){try{await ExecuteAsync("应用喷头参数和目标电流",()=>Client.WriteSettingsAsync(ReadSettings()));}catch(Exception ex){ShowError(ex);}}
    private async void ReadStatus_Click(object s,RoutedEventArgs e){try{ApplyStatus(await ExecuteAsync("读取控制器状态",()=>Client.ReadStatusAsync()));}catch(Exception ex){ShowError(ex);}}
    private async void Save_Click(object s,RoutedEventArgs e){if(MessageBox.Show("请确认水泵已关闭。是否保存当前参数？","保存确认",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;try{await ExecuteAsync("发送待保存参数",()=>Client.WriteSettingsAsync(ReadSettings() with{PersistenceEnabled=true}));await ExecuteAsync("保存参数",()=>Client.SaveToFlashAsync());}catch(Exception ex){ShowError(ex);}}
    private void ApplyStatus(ControllerStatus x){SetSwitch(x.PwmEnabled);StatusText.Text=$"PWM：{(x.PwmEnabled?"开启":"关闭")}；输出掩码：0x{x.OutputMask:X2}；通信保护：{(x.CommunicationStopped?"已触发":"正常")}；PWM节拍：{x.PwmTick}；目标电流：{x.TargetCurrentMicroamps/1000.0:F3} mA；模块回读：{x.CurrentModuleReadback/1000.0:F3} mA；故障次数：{x.CurrentModuleFaultCount}；固件：0x{x.FirmwareVersion:X4}";}
    private void SetSwitch(bool e){_syncSwitch=true;_confirmedEnabled=e;PwmMasterSwitch.IsChecked=e;_syncSwitch=false;}
    private async Task ExecuteAsync(string op,Func<Task> f)=>await ExecuteAsync(op,async()=>{await f();return true;});
    private async Task<T> ExecuteAsync<T>(string op,Func<Task<T>> f){var sw=Stopwatch.StartNew();AddLog($"发送：{op}");try{T result=await f();_ok++;SetResult($"控制器已回传确认：{op}，{sw.ElapsedMilliseconds} ms",Brushes.DarkGreen,"#E2F4E6");AddLog($"接收：确认“{op}”");Counters();return result;}catch{_fail++;SetResult($"发送失败：{op}",Brushes.DarkRed,"#FBE3E3");AddLog($"失败：{op}");Counters();throw;}}
    private void SetResult(string m,Brush f,string b){CommunicationResultText.Text=m;CommunicationResultText.Foreground=f;CommunicationResultBorder.Background=(Brush)new BrushConverter().ConvertFromString(b)!;}
    private void Counters()=>CommunicationCountersText.Text=$"成功 {_ok} 次；失败 {_fail} 次";
    private void AddLog(string m){_log.Add(OperationLogFormatter.Format(DateTime.Now,m));OperationLogList.ScrollIntoView(_log[^1]);}
    private void ClearLog_Click(object s,RoutedEventArgs e)=>_log.Clear();private void ShowError(Exception e){StatusText.Text=$"操作失败：{e.Message}";MessageBox.Show(e.Message,"操作失败",MessageBoxButton.OK,MessageBoxImage.Error);}
    protected override async void OnClosed(EventArgs e){_heartbeat.Stop();_lifetime.Cancel();if(_connection is not null)await _connection.DisposeAsync();_lifetime.Dispose();base.OnClosed(e);}
}
