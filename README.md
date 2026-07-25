# Honey 白玉蜘蛛桌面灵宠

Honey 是面向 Windows 11 x64 的桌面灵宠。首版提供白玉蜘蛛“小玉”：它会在桌面上自主观察、觅食、吐丝结网、玩耍、整理、睡眠，也会追踪鼠标并回应抚摸、拖动和技能指令。常态使用白玉与冷青材质，狂暴态转为黑玉与克制的赤色灵纹；体型、活跃程度、模式、专注模式、声音和可选 AI 个性均可设置。

项目借鉴修仙灵宠题材氛围，但不包含《凡人修仙传》动画的官方模型、贴图、音频或其他提取素材。

## 直接运行

下载 `Honey.exe` 后双击即可运行。发布包是 Windows 11 x64 自包含单文件，不要求预装 .NET。首次启动会在屏幕右下方显示小玉，并在系统托盘创建 Honey 图标。

常用操作：

- 左键拖动小玉可改变位置。
- 点击小玉会打开技能环；透明区域会穿透，不妨碍操作桌面。
- 托盘菜单可显示或隐藏、暂停、切换专注模式、打开设置和退出。
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

## 从源码发布

仓库要求 .NET 10 SDK。发布脚本优先使用仓库 `.dotnet\dotnet.exe`，也可显式指定 SDK；还原源仅作用于当前命令，不修改全局 NuGet 配置。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish.ps1 `
  -DotnetPath "C:\path\to\dotnet.exe"

powershell -NoProfile -ExecutionPolicy Bypass -File scripts\smoke-test.ps1 `
  -ExePath artifacts\win-x64\Honey.exe

powershell -NoProfile -ExecutionPolicy Bypass -File scripts\soak-test.ps1 `
  -ExePath artifacts\win-x64\Honey.exe -DurationHours 8
```

发布脚本会先还原并运行全部 Release 测试，再生成 `artifacts\win-x64\Honey.exe`，最后打印文件大小与 SHA-256。`artifacts\` 不进入 Git。
脚本会在安全校验后清空精确的输出目录，并递归确认交付目录最终只有根目录下的 `Honey.exe`。默认只允许仓库 `artifacts` 内的输出；确需发布到其他安全目录时必须显式传入 `-AllowExternalOutput`，盘符根目录和仓库根目录始终拒绝。

冒烟测试使用唯一隔离标识精确核对本次 `Honey.exe` 的进程数量，不会操作其他 Honey 进程；存档验收会实际打开 SQLite，执行 `quick_check`、`integrity_check`、架构与状态查询，并确认损坏探针会被拒绝。

浸泡测试默认 8 小时，每 30 秒采样一次。验收门槛为空闲平均 CPU 小于 1%、稳定工作集小于 150 MB、最后 4 小时线性增长小于 20 MB，并且进程无异常退出。开发时可用 `-DurationSeconds`、`-SampleSeconds` 和 `-WarmupSeconds` 做短时脚本验证；短时结果不能替代正式 8 小时验收。

第三方依赖及许可见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
