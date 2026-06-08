# SmartFill Monitor — 智能灌装监控上位机

基于 **.NET 8.0 + WPF + MVVM** 的工业上位机软件，通过 **Modbus RTU** 串口协议与 PLC 实时通信，实现灌装生产线的设备监控、数据采集、报警管理和生产追溯。
<img width="1578" height="871" alt="屏幕截图 2026-06-09 000221" src="https://github.com/user-attachments/assets/24a89654-8203-4e4c-9f88-f5d2db43baeb" />

---

## 功能概览

### 仪表盘
- 实时产量 / 温度 / 液位 / 节拍 四指标卡片
- 灌装工位动画 + 原料桶液位可视化
- 温度趋势实时曲线（LiveCharts）
- 最新报警列表 + 设备状态指示灯
- 一键启动 / 停止 / 复位
- <img width="3834" height="2101" alt="屏幕截图 2026-06-09 000722" src="https://github.com/user-attachments/assets/d62f7c49-e9a5-4e96-a1e7-f80fb461dcce" />



### Modbus RTU 通信
- NModbus4 串口管理，200ms 轮询
- FC03 读取保持寄存器 + FC06 写入命令
- SemaphoreSlim 异步锁保护串口互斥
- 实时通信状态检测：连续 3 次超时自动标记离线
- <img width="3832" height="2086" alt="屏幕截图 2026-06-09 000859" src="https://github.com/user-attachments/assets/78088447-e288-4cad-9d01-06b392aed1b4" />

### 报警管理
- 9 种报警码：低液位 / 溢出 / 泄漏 / 低压 / 高温 / 传感器故障 / 通信故障 / 系统错误
- 完整生命周期：触发 → 弹窗通知 → 确认 → 恢复
- 活动报警 + 历史查询
- <img width="1578" height="871" alt="屏幕截图 2026-06-09 000221" src="https://github.com/user-attachments/assets/2fb22f25-7b95-4358-a882-9f99879c3355" />

### 生产追溯
- 条码变化自动记录生产数据到 SQLite
- 日期范围查询 + 分页 + CSV 导出（CsvHelper, BOM）
- <img width="3832" height="2086" alt="屏幕截图 2026-06-09 000859" src="https://github.com/user-attachments/assets/7cae0b68-d025-4355-9b4b-7a22df19a0ab" />

### 日志系统
- Serilog 四路输出：RichTextBox 实时 / 文件滚动 / SQLite 持久化 / Console
- 按日期 + 级别 + 关键字组合查询
- <img width="3838" height="2096" alt="屏幕截图 2026-06-09 000734" src="https://github.com/user-attachments/assets/2fa68482-5e88-44f7-8b7c-62c32d9b1bb7" />

### 用户管理
- SHA256 + 盐密码哈希
- Admin / Engineer / Operator 角色权限
- 首次启动自动创建 admin 和 engineer 默认账户

---

## 技术栈

| 层 | 技术 |
|---|---|
| 框架 | WPF .NET 8.0, MVVM |
| MVVM 工具 | CommunityToolkit.Mvvm 8.4 |
| 数据库 | SQLite (FreeSql ORM + Serilog Sink) |
| 通信 | Modbus RTU (NModbus4), SerialPort |
| 图表 | LiveCharts 0.9.7 |
| UI 组件 | HandyControl 3.5, MahApps.Metro.IconPacks |
| 日志 | Serilog 4.3 |
| DI | Microsoft.Extensions.DependencyInjection |
| CSV | CsvHelper 33.1 |

---

## 项目结构

```
SmartFillMonitor/
├── Models/              数据实体 & Modbus 寄存器映射
├── Services/            业务服务 (PLC / 报警 / 用户 / 数据库)
├── ViewModels/          MVVM ViewModel 层
├── Views/               WPF XAML 视图
├── UserControls/        自定义控件 (导航 / 三色灯 / 状态指示)
├── Converters/          值转换器
├── Assests/Styles/      全局深色主题
├── App.xaml             应用入口
└── MainWindow.xaml      主窗口
```

---

## 运行环境

- .NET 8.0 Runtime（自包含发布无需安装）
- Windows 10 / 11 x64
- 虚拟串口 + Modbus Slave 模拟器（开发测试用）

---

## 快速开始

```bash
git clone https://github.com/saveload111/SmartFillMonitor.git
cd SmartFillMonitor
dotnet build
dotnet run --project SmartFillMonitor/SmartFillMonitor.csproj
```

首次启动自动创建 admin/admin 默认账户。

## 下载

前往 [Releases](https://github.com/saveload111/SmartFillMonitor/releases) 下载最新版，解压运行 `SmartFillMonitor.exe`。

---

## 架构图

```
┌─────────┐  Modbus RTU  ┌──────────────┐  Event  ┌──────────────────┐  Binding  ┌──────────┐
│   PLC   │◄────────────►│  PlcService   │────────►│   ViewModels      │──────────►│  Views   │
│ (从站1) │   FC03/FC06  │ (异步轮询)     │         │ (ObservableObject)│           │  (XAML)  │
└─────────┘              └──────────────┘         └──────────────────┘           └──────────┘
                                                          │
                                                          ▼
                                                   ┌──────────────┐
                                                   │    SQLite    │
                                                   │ (FreeSql ORM)│
                                                   └──────────────┘
```

---

