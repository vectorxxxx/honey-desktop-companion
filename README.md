<div align="center">

<p><strong>简体中文</strong> · <a href="README.en.md">English</a></p>

<h1>Honey</h1>
<p><strong>一只住在 Windows 桌面上的白玉蜘蛛灵宠</strong></p>

<p>
  <a href="https://github.com/vectorxxxx/honey-desktop-companion/releases/latest"><img src="https://img.shields.io/github/v/release/vectorxxxx/honey-desktop-companion?style=flat-square&amp;label=release&amp;color=7c3aed" alt="最新版本"></a>
  <a href="https://github.com/vectorxxxx/honey-desktop-companion/releases"><img src="https://img.shields.io/github/downloads/vectorxxxx/honey-desktop-companion/total?style=flat-square&amp;label=downloads&amp;color=0891b2" alt="总下载量"></a>
  <img src="https://img.shields.io/badge/Windows_11-x64-0078D4?style=flat-square&amp;logo=windows11&amp;logoColor=white" alt="Windows 11 x64">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&amp;logo=dotnet&amp;logoColor=white" alt=".NET 10">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-22c55e?style=flat-square" alt="MIT License"></a>
</p>

<p>
  <a href="https://github.com/vectorxxxx/honey-desktop-companion/releases/latest/download/Honey-win-x64-self-contained.exe"><strong>下载推荐版</strong></a>
  ·
  <a href="https://github.com/vectorxxxx/honey-desktop-companion/releases/latest/download/Honey-win-x64-framework-dependent.exe">下载轻量版</a>
  ·
  <a href="https://github.com/vectorxxxx/honey-desktop-companion/releases">版本记录</a>
</p>

<img src="src/Honey.Rendering/Assets/white-jade-spider-normal-atlas.png" width="680" alt="Honey 白玉蜘蛛十六方向预览">

</div>

## 特性

- 自主观察、移动、觅食、玩耍、整理和睡眠
- 鼠标追踪、拖动、抚摸与技能环交互
- 白玉常态与黑玉狂暴态
- 托盘控制、专注模式和个性设置
- 可选 AI 个性；关闭后仍可完整离线运行

`C# 14` · `.NET 10` · `WPF` · `SkiaSharp` · `SQLite`

## 快速开始

| 版本 | 运行要求 | 适合人群 |
| --- | --- | --- |
| `Honey-win-x64-self-contained.exe` | 无须安装 .NET | 推荐大多数用户下载 |
| `Honey-win-x64-framework-dependent.exe` | 需要 .NET 10 Desktop Runtime | 已安装运行时、希望减少下载量的用户 |

轻量版需要先安装运行时：

```powershell
winget install --id Microsoft.DotNet.DesktopRuntime.10 -e --source winget
```

1. 按需下载其中一个 EXE。
2. 双击运行，小玉会出现在桌面右下角。
3. 点击小玉打开技能环，拖动小玉改变位置。
4. 使用系统托盘中的 Honey 菜单进行设置或退出。

> 两个版本均为 Windows 11 x64 单文件程序，功能一致。

关闭设置窗口不会退出程序。请使用托盘菜单中的“退出”，或执行：

```powershell
.\Honey.exe --shutdown
```

## 命令行

| 命令 | 作用 |
| --- | --- |
| `.\Honey.exe --show` | 启动或显示小玉 |
| `.\Honey.exe --background` | 后台启动 |
| `.\Honey.exe --shutdown` | 保存状态并退出 |
| `.\Honey.exe --verify-data` | 验证本地存档 |
| `.\Honey.exe --show --data-root "D:\HoneyData"` | 使用独立数据目录启动 |

## 从源码构建

新电脑先安装 Git 和 .NET 10 SDK：

```powershell
winget install --id Git.Git -e --source winget
winget install --id Microsoft.DotNet.SDK.10 -e --source winget
```

重新打开 PowerShell，然后执行：

```powershell
git clone https://github.com/vectorxxxx/honey-desktop-companion.git
Set-Location .\honey-desktop-companion
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

脚本会自动还原依赖、运行 Release 测试并生成两个版本：

```text
artifacts\win-x64\Honey.exe
artifacts\win-x64-framework-dependent\Honey.exe
```

可选验证：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-test.ps1 `
  -ExePath .\artifacts\win-x64\Honey.exe
```

## 发布新版本

先安装并登录 GitHub CLI：

```powershell
winget install --id GitHub.cli -e --source winget
gh auth login
```

构建完成后，修改版本号并发布：

```powershell
$version = "v1.0.2"
git tag -a $version -m "发布：Honey $version"
git push origin $version
$releaseDir = ".\artifacts\release"
New-Item -ItemType Directory -Force $releaseDir | Out-Null
Copy-Item .\artifacts\win-x64\Honey.exe `
  "$releaseDir\Honey-win-x64-self-contained.exe"
Copy-Item .\artifacts\win-x64-framework-dependent\Honey.exe `
  "$releaseDir\Honey-win-x64-framework-dependent.exe"
gh release create $version "$releaseDir\*.exe" `
  --verify-tag --generate-notes --latest
```

已发布的标签不要覆盖；代码变化后创建新版本号。

## 数据与隐私

数据默认保存在 `%LOCALAPPDATA%\Honey\`，包括 SQLite 存档、设置和日志。

AI 功能默认关闭，后台不会主动请求 AI。启用后只发送必要的状态摘要，不发送屏幕截图、文件内容或鼠标轨迹；API 密钥使用 Windows DPAPI 加密。

## 素材与许可

项目借鉴修仙灵宠氛围，但不包含《凡人修仙传》动画的官方模型、贴图、音频或其他提取素材。

本项目原创代码与素材采用 [MIT License](LICENSE)。第三方依赖见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)，许可原文见 [LICENSES](LICENSES)。
