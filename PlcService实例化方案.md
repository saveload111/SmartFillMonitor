# PlcService 去掉 static — 最简方案

## 只动三个文件 + 一个调用点

================================================================
                        PlcService.cs
================================================================

1. 类声明去 static
   public class PlcService  ← 删掉 static

2. 字段和事件去 static
   所有的 private static → private
   所有的 public static event → public event

3. 方法签名去 static
   所有 public static async Task → public async Task
   GetAvailablePorts / ParseParity / ParseStop 保留 static

4. 加 IDisposable
   public sealed class PlcService : IDisposable
   {
       public void Dispose() => DisConnectAsync().GetAwaiter().GetResult();

5. 空构造函数留着（你原来有）
   public PlcService() { }

================================================================
                        App.xaml.cs
================================================================

原来用的是全局 static，改成创建实例：

private PlcService? _plcService;

OnStartup 里：
_plcService = new PlcService();
await _plcService.Initialize(plcSettings);

OnExit 里：
if (_plcService != null)
    await _plcService.DisConnectAsync();

MainWindow 构造时把实例传进去：
var mainVM = ServiceProvider.GetRequiredService<MainWindowViewModel>();
mainVM.SetPlcService(_plcService);   ← 新增这个方法

================================================================
                   MainWindowViewModel.cs
================================================================

加字段和方法：
private PlcService? _plc;

public void SetPlcService(PlcService plc)
{
    _plc = plc;
    plc.ConnectionChanged += (s, online) => IsPlcConnected = online;
    plc.DataReceived += PlcService_DataReceived;
}

================================================================
                   DashBoardViewModel.cs
================================================================

同理，加 SetPlcService：
public void SetPlcService(PlcService plc)
{
    plc.DataReceived += OnDataReceived;
}
StartProductionAsync / StopProductionAsync / ResetProductionAsync 里的
PlcService.WriteCommandStateAsync 全部改成 plc.WriteCommandStateAsync

构造函数里去掉 PlcService.DataReceived += OnDataReceived
（改成外面调 SetPlcService）

================================================================
                      调用链路
================================================================

App.OnStartup
  → new PlcService()
  → plc.Initialize(settings)
  → 拿 mainVM.SetPlcService(plc)     传进去
  → 拿 dashboardVM.SetPlcService(plc) 传进去

================================================================
                      改多少行
================================================================

PlcService:    删 30 个 static 关键字 + 加 Dispose，20 行
App.xaml.cs:   存字段 + 传实例，10 行
MainWindowVM:  加 SetPlcService，10 行
DashboardVM:   加 SetPlcService，改命令引用，15 行

总计 55 行。
