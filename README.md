# Honey 白玉蜘蛛桌面灵宠

Honey 是面向 Windows 11 x64 的桌面灵宠。首版提供白玉蜘蛛“小玉”：它会在桌面上自主观察、觅食、吐丝结网、玩耍、整理、睡眠，也会追踪鼠标并回应抚摸、拖动和技能指令。常态使用白玉与冷青材质，狂暴态转为黑玉与克制的赤色灵纹；体型、活跃程度、模式、专注模式、声音和可选 AI 个性均可设置。

项目借鉴修仙灵宠题材氛围，但不包含《凡人修仙传》动画的官方模型、贴图、音频或其他提取素材。

## 直接运行

下载 `Honey.exe` 后双击即可运行。发布包是 Windows 11 x64 自包含单文件，不要求预装 .NET。首次启动会在屏幕右下方显示小玉，并在系统托盘创建 Honey 图标。

常用操作：

- 左键拖动小玉可改变位置。
- 点击小玉会打开技能环；透明区域会穿透，不妨碍操作桌面。
- 托盘菜单可显示或隐藏、暂停、切换专注模式、打开设置和退出。
- 设置窗口采用独立的墨玉暗色主题，控件配色与交互状态不受 Windows 明暗主题影响。
- 必须使用托盘菜单中的“退出”或 `Honey.exe --shutdown` 才会结束后台进程；关闭设置窗口不会退出灵宠。

## 启动命令

```powershell
Honey.exe --background
Honey.exe --show
Honey.exe --shutdown
Honey.exe --verify-data
Honey.exe --show --data-root "D:\HoneyData"
```

- `--background`：在后台启动，不主动显示灵宠窗口。
- `--show`：启动新实例，或通知已有实例显示灵宠。
- `--shutdown`：请求已有实例保存状态并安全退出；没有实例时会快速成功，不创建存档。
- `--verify-data`：无界面、无托盘地只读验证当前数据目录中的 SQLite 存档，成功返回退出码 0，损坏或缺少状态返回退出码 4。
- `--data-root <目录>`：使用独立的数据、设置、密钥和单实例通道，适合测试或便携隔离。也可设置环境变量 `HONEY_DATA_ROOT`。

同一数据目录只允许一个实例。第二次运行不会重复初始化数据库、托盘或窗口。

## AI 个性与隐私

AI 增强默认关闭，不影响本地自主行为。只有在设置页主动测试连接或点击“灵感”时才会发起请求；后台待机不会自行访问 AI。发送内容仅包含本次简短提示、小玉当前状态摘要和有限记忆摘要，不包含屏幕截图、文件内容、鼠标轨迹或数据库。

API 地址、模型与启用状态保存在设置文件；API 密钥使用 Windows 当前用户 DPAPI 加密，并与地址和模型绑定。密钥不会写入日志。支持 OpenAI Chat Completions 兼容接口，可在设置中配置 HTTPS 地址、模型和密钥；仅允许回环地址使用明文 HTTP。错误密钥、断网、超时、限流或服务故障时，小玉会安全降级为完整的本地自主循环。

## 数据与故障日志

默认数据目录为：

```text
%LOCALAPPDATA%\Honey\
```

其中：

- `honey.db`：SQLite 状态与位置存档。
- `settings.json`：非敏感设置。
- `secrets.json`：DPAPI 加密后的 AI 密钥绑定。
- `logs\honey.log`：可供排查的故障日志，不记录 API 密钥。

删除数据前请先退出 Honey。需要全新体验时可备份后移走整个目录。

## 在新电脑上从源码构建

以下流程适用于 Windows 11 x64。构建电脑需要 Git、PowerShell 5.1 或更高版本，以及 .NET 10 SDK；生成的 `Honey.exe` 是自包含程序，分发到其他 Windows 11 x64 电脑后不需要安装 .NET。

### 1. 安装构建环境

以管理员身份打开 PowerShell，使用 WinGet 安装 Git 和 .NET 10 SDK：

```powershell
winget install --id Git.Git -e --source winget
winget install --id Microsoft.DotNet.SDK.10 -e --source winget
```

安装后关闭并重新打开 PowerShell，再确认命令可用：

```powershell
git --version
dotnet --version
dotnet --list-sdks
```

仓库的 `global.json` 要求 .NET SDK `10.0.100`，并允许使用同一主版本内更新的 10.0 SDK。只需安装 SDK，无须单独安装 .NET Desktop Runtime。安装方式可参考 [Git for Windows](https://git-scm.com/install/windows) 和 [Microsoft 的 .NET Windows 安装说明](https://learn.microsoft.com/zh-cn/dotnet/core/install/windows)。

### 2. 获取最新源码

```powershell
git clone https://github.com/vectorxxxx/20260724-honey.git
Set-Location .\20260724-honey
git switch main
git pull --ff-only origin main
git status --short --branch
```

最后一条命令应显示当前位于 `main`，且没有未提交文件。以后重新构建前，只需进入仓库并执行 `git pull --ff-only origin main` 获取最新代码。

### 3. 一键生成 Honey.exe（推荐）

在仓库根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

脚本会依次执行 NuGet 还原、全部 Release 测试和 Windows x64 自包含单文件发布。成功后输出：

```text
artifacts\win-x64\Honey.exe
```

脚本最后还会打印文件大小与 SHA-256。可以再次核对产物：

```powershell
Get-Item .\artifacts\win-x64\Honey.exe |
  Select-Object FullName, Length, LastWriteTime
Get-FileHash .\artifacts\win-x64\Honey.exe -Algorithm SHA256
```

如果 `dotnet.exe` 没有加入 `PATH`，可显式传入 SDK 路径：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish.ps1 `
  -DotnetPath "C:\Program Files\dotnet\dotnet.exe"
```

发布脚本也会优先识别仓库内的 `.dotnet\dotnet.exe`，适合使用免管理员安装的本地 SDK。

### 4. 验证可执行文件（可选）

短时自动冒烟测试会使用隔离的数据目录，不会覆盖日常存档：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-test.ps1 `
  -ExePath .\artifacts\win-x64\Honey.exe
```

需要执行正式的 8 小时稳定性验收时运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\soak-test.ps1 `
  -ExePath .\artifacts\win-x64\Honey.exe -DurationHours 8
```

也可以直接双击 `artifacts\win-x64\Honey.exe`，或在 PowerShell 中使用独立数据目录启动：

```powershell
& .\artifacts\win-x64\Honey.exe --show `
  --data-root "$env:TEMP\HoneyBuildCheck"
```

验证结束后使用同一数据目录安全退出：

```powershell
& .\artifacts\win-x64\Honey.exe --shutdown `
  --data-root "$env:TEMP\HoneyBuildCheck"
```

### 5. 常见构建问题

- 提示 `A compatible .NET SDK was not found`：重新打开 PowerShell，执行 `dotnet --list-sdks`，确认存在 10.0 SDK，并检查当前目录是否为仓库根目录。
- 提示找不到 `dotnet`：执行 `where.exe dotnet`；仍找不到时重启终端，或通过 `-DotnetPath` 指定 `C:\Program Files\dotnet\dotnet.exe`。
- 出现 `NU1301`、还原超时或 TLS 错误：确认电脑可以访问 `https://api.nuget.org/v3/index.json`，并检查公司代理、防火墙和系统时间。
- PowerShell 阻止脚本执行：使用文档中的 `-ExecutionPolicy Bypass` 命令；不需要修改系统的永久执行策略。
- 已存在的发布目录被拒绝：不要手动向 `artifacts\win-x64` 放入文件。可改用新的安全子目录，例如 `-Output artifacts\win-x64-local`。

`artifacts\` 已被 Git 忽略，不会进入源码提交。发布脚本只允许仓库 `artifacts` 的严格子目录作为输出，拒绝外部目录、仓库根目录、`artifacts` 根目录及任何重解析点。输出目标由绑定规范路径的版本化所有权标记保护；未标记的非空目录绝不会被接管或清空。构建先进入唯一暂存目录，递归确认仅含根目录 `Honey.exe` 后才替换受控目标。

冒烟测试使用唯一隔离标识精确核对本次 `Honey.exe` 的进程数量，不会操作其他 Honey 进程；存档验收会复制数据库及 WAL/SHM 到隔离临时目录，再执行 `quick_check`、`integrity_check`、架构与状态查询，原存档目录的文件、内容与时间戳保持不变。

单实例互斥体沿用 Windows 当前用户默认 DACL，并把当前用户 SID 与数据目录的物理规范路径共同纳入名称哈希；命名管道同时启用 `CurrentUserOnly`。

浸泡测试默认 8 小时，每 30 秒采样一次。验收门槛为空闲平均 CPU 小于 1%、稳定工作集小于 150 MB、最后 4 小时线性增长小于 20 MB，并且进程无异常退出。开发时可用 `-DurationSeconds`、`-SampleSeconds` 和 `-WarmupSeconds` 做短时脚本验证；短时结果不能替代正式 8 小时验收。

## 发布可执行文件到 GitHub Releases

不要把 `Honey.exe` 提交进 Git 历史；应把它作为对应版本的 GitHub Release 附件上传。建议使用语义化版本标签，例如正式版 `v1.0.0`、修复版 `v1.0.1`、预发布版 `v1.1.0-beta.1`。以下示例发布 `v1.0.0`，实际操作时请替换为本次版本号。

### 1. 安装并登录 GitHub CLI

```powershell
winget install --id GitHub.cli -e --source winget
gh auth login
gh auth status
```

`gh auth login` 中选择 `GitHub.com`，按提示通过浏览器登录，并授权能够写入该仓库的 GitHub 账号。相关命令见 [GitHub CLI](https://cli.github.com/) 和 [`gh release create` 手册](https://cli.github.com/manual/gh_release_create)。

### 2. 确认版本源码与可执行文件

先确保 `main` 已同步、工作区干净，再执行正式构建和冒烟测试：

```powershell
git switch main
git pull --ff-only origin main
git status --short --branch

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke-test.ps1 `
  -ExePath .\artifacts\win-x64\Honey.exe
```

为下载者同时生成 SHA-256 校验文件：

```powershell
$exeHash = (Get-FileHash .\artifacts\win-x64\Honey.exe `
  -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath .\artifacts\Honey.exe.sha256 `
  -Value "$exeHash  Honey.exe" -Encoding Ascii
```

### 3. 创建并推送版本标签

标签必须指向本次已经验证的 `main` 提交：

```powershell
git tag -a v1.0.0 -m "发布：Honey v1.0.0"
git push origin v1.0.0
git show --no-patch --decorate v1.0.0
```

已发布的版本标签不应移动或复用。代码发生变化后应递增版本号并创建新标签。

### 4. 创建 Release 并上传附件

推荐先创建草稿，确认标题、说明和附件无误后再公开：

```powershell
gh release create v1.0.0 `
  .\artifacts\win-x64\Honey.exe `
  .\artifacts\Honey.exe.sha256 `
  --verify-tag `
  --title "Honey v1.0.0" `
  --generate-notes `
  --draft

gh release view v1.0.0 --web
```

浏览器会打开 Release 草稿。检查附件可以下载、更新说明正确后，点击“Publish release”；也可以直接用命令发布草稿：

```powershell
gh release edit v1.0.0 --draft=false --latest
gh release view v1.0.0
```

预发布版本应在创建时增加 `--prerelease`，不要标记为 Latest。GitHub 官方的网页端流程见 [管理仓库中的 Release](https://docs.github.com/zh/repositories/releasing-projects-on-github/managing-releases-in-a-repository)。

### 5. 网页端发布方式

不使用 `gh` 时，可以打开仓库主页，进入 **Releases**，选择 **Draft a new release**，然后：

1. 选择已经推送的标签，例如 `v1.0.0`，目标提交选择 `main` 对应的已验证提交。
2. 填写标题和更新说明；预览版勾选 **Set as a pre-release**。
3. 将 `artifacts\win-x64\Honey.exe` 和 `artifacts\Honey.exe.sha256` 拖到附件区域。
4. 先保存草稿进行复核，确认后点击 **Publish release**。

仓库为公开仓库时，任何人都可下载 Release 附件；私有仓库只允许具备相应权限的用户下载。当前程序若未配置受信任的 Windows 代码签名证书，其他电脑首次下载运行时可能出现“未知发布者”或 SmartScreen 提示；GitHub Release 和 SHA-256 只能帮助确认版本与文件完整性，不能替代代码签名。

第三方依赖与包到条款的映射见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)，完整许可原文与上游第三方声明位于 [`LICENSES`](LICENSES)；两者均嵌入最终 `Honey.exe`。
