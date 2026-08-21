# 第三方声明

**简体中文** · [English](THIRD-PARTY-NOTICES.en.md)

## Lucide 图标

Honey 的桌面环形菜单与设置界面使用并按界面尺寸改编了 Lucide 的 Heart、Pause、Play、Moon、Zap、X、Settings、Sparkles、Monitor、Brain、Save 与 RotateCcw 线性图标路径。Lucide 项目采用 ISC License。本项目与 Lucide 官方不存在背书或隶属关系。

以下许可原文按法律文本原样保留：

```text
ISC License

Copyright (c) 2022 Lucide Contributors

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
```

## Windows x64 自包含运行时依赖

以下清单依据 `project.assets.json`、最终 `Honey.deps.json` 与本机构建所用 NuGet 包中的 `.nuspec`、`LICENSE`、`COPYING` 和 `THIRD-PARTY-NOTICES` 元数据核对。版本均为本次 Windows x64 自包含单文件发布实际解析的版本。

### SkiaSharp 4.150.1

包含 `SkiaSharp`、`SkiaSharp.Views.WPF`、`SkiaSharp.Views.Desktop.Common`、`SkiaSharp.NativeAssets.Win32`。

- 许可证：MIT
- 版权：Copyright (c) 2015-2016 Xamarin, Inc.; Copyright (c) 2017-2018 Microsoft Corporation.
- 原生组件声明：`SkiaSharp.NativeAssets.Win32` 包内 `THIRD-PARTY-NOTICES.txt`，SHA-256 `21504C46C4C58AA64C1055BD2DCBC5F9A136B4B8C412ED3CC6740E22C5B127F5`。该声明涵盖 ANGLE、HarfBuzz、Skia、libpng、FreeType、ICU、libjpeg-turbo、WebP 与 zlib 等原生组件。

完整 MIT 条款见 `LICENSES/MIT-SkiaSharp.txt`；原生组件完整声明见 `LICENSES/SkiaSharp.NativeAssets.Win32-THIRD-PARTY-NOTICES.txt`。

### SQLite

`Microsoft.Data.Sqlite 10.0.10` 与 `Microsoft.Data.Sqlite.Core 10.0.10` 采用 MIT License，版权为 © Microsoft Corporation. All rights reserved.

`SQLitePCLRaw.bundle_e_sqlite3`、`SQLitePCLRaw.core`、`SQLitePCLRaw.provider.e_sqlite3`、`SQLitePCLRaw.lib.e_sqlite3` 均为 2.1.12，采用 Apache License 2.0，版权为 Copyright 2014-2024 SourceGear, LLC.

完整 Apache License 2.0（包括第 4(d) 节 NOTICE 保留要求）见 `LICENSES/Apache-2.0.txt`。上述四个 SQLitePCLRaw NuGet 包不含独立 NOTICE 文件。随 `SQLitePCLRaw.lib.e_sqlite3` 分发的 SQLite 原生库属于公共领域，其 dedication 与 blessing 见 `LICENSES/SQLite-Public-Domain.txt`。

### Microsoft.Extensions 10.0.10

以下 27 个包均采用 MIT License，版权为 © Microsoft Corporation. All rights reserved.

- Configuration：`Microsoft.Extensions.Configuration`、`.Abstractions`、`.Binder`、`.CommandLine`、`.EnvironmentVariables`、`.FileExtensions`、`.Json`、`.UserSecrets`
- DependencyInjection：`Microsoft.Extensions.DependencyInjection`、`.Abstractions`
- Diagnostics：`Microsoft.Extensions.Diagnostics`、`.Abstractions`
- FileProviders：`Microsoft.Extensions.FileProviders.Abstractions`、`.Physical`、`Microsoft.Extensions.FileSystemGlobbing`
- Hosting：`Microsoft.Extensions.Hosting`、`.Abstractions`
- Logging：`Microsoft.Extensions.Logging`、`.Abstractions`、`.Configuration`、`.Console`、`.Debug`、`.EventLog`、`.EventSource`
- Options 与基础类型：`Microsoft.Extensions.Options`、`.ConfigurationExtensions`、`Microsoft.Extensions.Primitives`

上述包携带相同的 `THIRD-PARTY-NOTICES.TXT`，SHA-256 为 `6D15E10A101C6BFFF2AB4429ED061BF76C456FC4B23AD6B03E0D0F8377148A21`。

Microsoft MIT 完整条款见 `LICENSES/MIT-Microsoft.NET.txt`；完整第三方声明见 `LICENSES/Microsoft.NET-THIRD-PARTY-NOTICES.txt`。

### OpenTK、GLWpfControl 与 GLFW

SkiaSharp WPF 视图传递引入：

- `OpenTK`、`OpenTK.Compute`、`OpenTK.Core`、`OpenTK.Graphics`、`OpenTK.Input`、`OpenTK.Mathematics`、`OpenTK.OpenAL`、`OpenTK.Windowing.Common`、`OpenTK.Windowing.Desktop`、`OpenTK.Windowing.GraphicsLibraryFramework`，版本 4.3.0。OpenTK 4.3.0 元包声明 MIT License；版权为 Copyright (c) 2006 - 2020 Stefanos Apostolopoulos for the Open Toolkit library。
- `OpenTK.GLWpfControl 4.2.3`。NuGet 元数据以项目 `LICENSE.md` 为许可来源；版权为 Copyright (c) 2022 Team OpenTK。
- `OpenTK.redist.glfw 3.3.0-pre20200830200122`。包内 `COPYING.md` 为 zlib 风格许可，SHA-256 `8EA14FDC7EFEE7FE53C79101B97049BD547DC6686CFA05DF4F0686146A561423`；版权为 Copyright (c) 2002-2006 Marcus Geelnard 与 Copyright (c) 2006-2016 Camilla Löwy。

OpenTK、GLWpfControl、GLFW 的完整条款分别见 `LICENSES/MIT-OpenTK.md`、`LICENSES/OpenTK-THIRD_PARTIES.md`、`LICENSES/MIT-GLWpfControl.md` 与 `LICENSES/Zlib-GLFW.md`。

### .NET 10.0.10 自包含运行时

`runtimepack.Microsoft.NETCore.App.Runtime.win-x64` 与 `runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64` 采用 MIT License；版权分别归 Microsoft Corporation、.NET Foundation and Contributors。NETCore 运行时第三方声明与上述 Microsoft.Extensions 声明内容一致。

完整条款分别见 `LICENSES/MIT-Microsoft.NET.txt`、`LICENSES/MIT-WindowsDesktop.txt` 与 `LICENSES/Microsoft.NET-THIRD-PARTY-NOTICES.txt`。

`Microsoft.NET.ILLink.Tasks 10.0.10` 仅为 MIT 许可的构建期工具，不包含在最终运行时依赖中。`runtimepack.Microsoft.Windows.SDK.NET.Ref 10.0.19041.57` 仅为引用期包，不随 `Honey.exe` 分发。

本文件中的 Lucide ISC 条款以及 `LICENSES` 下全部许可与第三方声明均作为嵌入资源包含在 `Honey.exe` 中。
