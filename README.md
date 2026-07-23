# Astro Device Hub

统一控制电动 CAA、电动调焦器、电动平场板与电动滤镜轮的 Windows 桌面程序。图形界面、设备 Server、ASCOM 桥接与固件刷写集中在同一产品中。

## 当前支持

- CAA：支持两个并发应用客户端；状态、回零、停止、绝对移动与设零。
- 调焦器：支持一个应用客户端；状态、回零、停止、绝对移动与设零。
- 平场板：支持一个应用客户端；盖板开合、停止、亮度设置与关灯。
- 滤镜轮：支持一个应用客户端；状态、回零、停止、槽位查询与转动。
- 固件中心：HEX/BIN 文件归档、SHA-256 校验，以及 Arduino CLI 刷写流程。
- 一体式桌面窗口：WPF 原生窗口承载 WebView2 控制台，并在同一进程启动设备 Server。
- 实际固件刷写：通过 Arduino CLI 支持 Arduino Nano、Nano Old Bootloader 与 ESP8266，上传后执行校验。

## 启动桌面程序

```powershell
cd D:\Unity\AstroDeviceHub
dotnet run --project .\desktop\AstroDeviceHub.Desktop.csproj
```

发布版主程序位于 `dist\app\AstroDeviceHub.App.exe`。桌面程序启动后会自动启动本地设备服务，日常使用无需配置服务地址或端口。

无界面 Server 入口保留在 `dist\server\AstroDeviceHub.exe`，用于后台服务部署。服务默认仅接受本机访问。

局域网远程控制位于桌面程序 Server 页面的高级区域。启用后需要设置访问令牌，并在重启桌面程序后生效。

## 构建与 ASCOM 注册

```powershell
.\Build.ps1
# 在管理员 PowerShell 中：
.\Register-Ascom.ps1
```

注册后，ASCOM Chooser 中会出现以下四个驱动：

- `InECAA-Device1` 至 `InECAA-Device3`
- `InEFucoser-Device1` 至 `InEFucoser-Device3`
- `InDLCoverCalibrator-Device1` 至 `InDLCoverCalibrator-Device3`
- `InEFilterWheel-Device1` 至 `InEFilterWheel-Device3`

每类设备最多可添加三台。设备按添加顺序占用 Device1 至 Device3；删除后槽位保留为空，不会自动重排，因此 NINA 重启后仍会选择原先保存的 Driver ID。桌面程序关闭时，发布目录中的 ASCOM 驱动可以自动启动随附的本地 Hub 服务。

## 安装包

运行以下命令生成 Windows 安装包：

```powershell
.\Build-Installer.ps1
```

安装包位于 `dist\installer`。安装程序会检查 ASCOM Platform 6 或 7，请求管理员权限，并自动注册四个 ASCOM 驱动；卸载时会自动注销它们。开发环境中直接使用发布目录时，仍可执行 `Register-Ascom.ps1` 完成手动注册。

## 连接配额

| 类型 | 上限 |
| --- | --- |
| 电动 CAA | 2 个应用客户端，例如 NINA、PHD2 |
| 电动调焦器 | 1 个应用客户端 |
| 电动平场板 | 1 个应用客户端 |
| 电动滤镜轮 | 1 个应用客户端 |

每个设备会话由服务端串行化命令。多个获准客户端通过服务端提交请求，服务端保持一条物理设备连接。

每个连接及控制请求都带 `clientId`。服务端只接受已获租约的客户端控制设备；因此 NINA、PHD2、控制台和未来 ASCOM/Alpaca 适配器均可被准确计数。
