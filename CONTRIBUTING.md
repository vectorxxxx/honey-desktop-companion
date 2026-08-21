# 参与贡献

感谢你帮助 Honey 变得更好。提交代码前，请先通过 Issue 描述问题；功能构想和使用交流可以放到 Discussions。

## 开发环境

需要 Windows 11、Git 和 .NET 10 SDK：

```powershell
winget install --id Git.Git -e --source winget
winget install --id Microsoft.DotNet.SDK.10 -e --source winget
git clone https://github.com/vectorxxxx/honey-desktop-companion.git
Set-Location .\honey-desktop-companion
dotnet restore Honey.slnx
dotnet test Honey.slnx -c Release --no-restore
```

生成两种 Windows x64 单文件版本：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

## 提交 Pull Request

1. 从最新 `main` 创建目标明确的分支。
2. 保持改动聚焦，并为行为变化补充测试。
3. 使用中文提交信息，清楚说明改动目的。
4. 确认 Release 测试和 PowerShell 回归脚本通过。
5. 在 Pull Request 中说明影响范围、验证方法和关联 Issue。

界面改动请附截图或短视频。性能相关改动请提供前后数据及测试环境。

## 隐私与素材

- 不要提交密钥、令牌、个人路径、日志、数据库或真实用户数据。
- 新增图片、音频、字体或模型时，必须说明来源和许可证。
- 不接受从商业作品中提取或权属不明的素材。

提交贡献即表示你同意相关内容按项目的 [MIT License](LICENSE) 发布。
