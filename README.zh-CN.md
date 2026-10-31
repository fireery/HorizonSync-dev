<div align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/HorizonSync-White.svg">
    <img src="assets/HorizonSync-Black.svg" alt="HorizonSync 标志" width="100">
  </picture>
</div>

<h1 align="center">HorizonSync</h1>
<p align="center">让壁纸跟着日出日落。</p>

<div align="center">

[![GitHub](https://img.shields.io/badge/GitHub-repository-24292f?style=flat-square&logo=github)](https://github.com/fireery/HorizonSync-dev)
[![Build](https://github.com/fireery/HorizonSync-dev/actions/workflows/ci.yml/badge.svg)](https://github.com/fireery/HorizonSync-dev/actions/workflows/ci.yml)
[![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4?style=flat-square)](#运行要求)
[![License](https://img.shields.io/badge/License-GPL--3.0-205c61?style=flat-square)](LICENSE)
[![Development](https://img.shields.io/badge/Development-0.2.0--preview-8c6b37?style=flat-square)](https://github.com/fireery/HorizonSync-dev/pull/2)

</div>

<p align="center"><a href="README.md">English</a> · <b>简体中文</b></p>
<p align="center"><a href="https://github.com/fireery/HorizonSync-dev/releases">下载</a> · <a href="docs/USAGE.zh-CN.md">使用说明</a> · <a href="https://github.com/fireery/HorizonSync-dev/issues">问题反馈</a></p>

## 简介

HorizonSync 是配合 **Wallpaper Engine** 使用的轻量 Windows 工具。它根据每天的日出日落，切换白天与夜晚壁纸，让切换时间随季节变化。

日出日落在本地计算，程序通过 Wallpaper Engine 官方命令接口进行控制，独立运行。

## 功能

- **随日出日落切换**：使用系统定位或指定坐标，分别设置日出、日落的提前或延后分钟数。
- **跟随系统时间**：以 Windows 当前日期、时间和时区为准，处理夏令时变化。
- **自动查找程序**：优先使用已保存的安装位置，显示就绪状态；异常入口直接跳转对应设置。
- **选择昼夜壁纸**：分别选择白天和夜晚壁纸，也可使用固定时间切换。
- **托盘运行**：支持登录启动，以及唤醒、解锁和 Wallpaper Engine 重启后的重新对齐。
- **中英双语**：提供跟随系统、简体中文和 English，切换后即时更新。
- **本地配置**：支持用户目录和便携模式、配置备份与损坏文件保留，不包含遥测。

## 界面预览

<p align="center"><img src="docs/images/time-en.png" alt="HorizonSync 时间设置" width="900"></p>

*截图使用示例坐标。开发分支新增缩略图选择与居中 GIF 大图预览；已发布预览包仍为原版本。*

## 下载

从 [GitHub Releases](https://github.com/fireery/HorizonSync-dev/releases) 获取**安装包**或**便携 ZIP**，同时提供 SHA-256 校验值和对应源码包。

首个版本为**开发预览版**。真实壁纸切换场景、安装与卸载等尚未完成的验证，见[验证记录](docs/VALIDATION.md)。

### 运行要求

- Windows 10 Creators Update 或更新版本，或 Windows 11。
- .NET Framework 4.8 或更新版本。
- 已安装并运行 Wallpaper Engine，两个程序由相同用户、以匹配的权限运行。

当前版本每次管理一个 Wallpaper Engine 显示器编号。日出日落为标准模型估算，地形及天气会影响实际可见时间。

## 开始使用

1. 在**时间**页选择切换方式，核对位置并预览今日计划。
2. 在**壁纸**页检查程序状态，选择并测试白天、夜晚壁纸。
3. 保存原 Wallpaper Engine 播放列表，停用其中的定时切换，避免两套规则覆盖彼此。
4. 打开启用开关，点击**保存设置**。

关闭窗口后程序留在托盘；需要停止时，在托盘菜单选择**彻底退出**。配置、恢复与卸载方法见[使用说明](docs/USAGE.zh-CN.md)。

## 参与贡献

欢迎提交错误报告、功能建议、代码改进和翻译。

- [报告问题或提出建议](https://github.com/fireery/HorizonSync-dev/issues/new)。
- [提交 Pull Request](https://github.com/fireery/HorizonSync-dev/compare)。
- 较大改动请先查看[迭代记录](docs/ROADMAP.md)。

在 Windows 上构建和测试：

```powershell
.\build.ps1
.\tests\Run-Tests.ps1
```

可用 Visual Studio 打开 `HorizonSync.csproj`，也可使用项目编译脚本。程序仅依赖系统 .NET Framework 库。便携包、安装包的构建方法见[发行检查](docs/RELEASE.md)。

## 许可证

HorizonSync 按 **[GNU GPL v3.0 only](LICENSE)**（`GPL-3.0-only`）发布，不提供担保。仓库提供对应源代码和构建脚本。

Copyright © 2026 fireery and HorizonSync contributors.

## 致谢

- [Wallpaper Engine](https://help.wallpaperengine.io/en/functionality/cli.html)：官方控制接口。
- [NOAA](https://gml.noaa.gov/grad/solcalc/calcdetails.html)：太阳位置计算参考。
- [HMCL](https://github.com/HMCL-dev/HMCL)：README 版式参考。

HorizonSync 为独立项目，不捆绑 Wallpaper Engine 或 Auto Dark Mode。
