# Rolan Next

Rolan Next 是一个 Windows 桌面启动台，用 C# 和 WPF（Windows Presentation Foundation，Windows 界面框架）开发。它支持分组管理、拖放排序、中文拼音搜索、主题配色和屏幕边缘停靠。搜索结果可跳回所在分组，并短暂高亮对应启动项。面板可在鼠标移出后自动隐藏。

## 使用

1. 将 Windows x64 便携版完整解压到有写入权限的目录，运行 `RolanNext.exe`。自包含构建已带上 .NET 桌面运行环境。
2. 首次运行从空白启动台开始。可拖入程序或快捷方式，也可以通过主菜单导入旧版 Rolan 配置或新配置备份。
3. 启动项和设置默认保存在程序旁的 `Data` 中。迁移或升级前退出程序并备份 `Data`。迁到另一台电脑后，按需调整启动项路径。

## 构建和验证

在兼容的 64 位 Windows 上安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)，用 PowerShell 在仓库根目录执行：

```powershell
./verify.ps1
./build.ps1 -Pack
```

`verify.ps1` 使用测试时生成的示例数据，验证配置导入、搜索定位、菜单状态和窗口行为。`build.ps1 -Pack` 在 `artifacts` 生成自包含便携 ZIP。构建和验证产物都在本地生成。

## 第三方数据

拼音数据随附 [Unicode License V3](src/Rolan.Core/Unicode-LICENSE.txt)，请保留完整的许可和版权声明。
