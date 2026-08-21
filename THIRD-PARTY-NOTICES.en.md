# Third-Party Notices

[简体中文](THIRD-PARTY-NOTICES.md) · **English**

## Lucide icons

Honey uses and adapts the Lucide Heart, Pause, Play, Moon, Zap, X, Settings, Sparkles, Monitor, Brain, Save, and RotateCcw line icon paths for its desktop radial menu and settings interface. Lucide is licensed under the ISC License. Honey is not endorsed by or affiliated with Lucide.

The license text is preserved verbatim below:

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

## Windows x64 self-contained runtime dependencies

This inventory was verified against `project.assets.json`, the final `Honey.deps.json`, and the `.nuspec`, `LICENSE`, `COPYING`, and `THIRD-PARTY-NOTICES` metadata in the NuGet packages used for the build. Versions are those resolved by the Windows x64 self-contained single-file release.

### SkiaSharp 4.150.1

Includes `SkiaSharp`, `SkiaSharp.Views.WPF`, `SkiaSharp.Views.Desktop.Common`, and `SkiaSharp.NativeAssets.Win32`.

- License: MIT
- Copyright: Copyright (c) 2015-2016 Xamarin, Inc.; Copyright (c) 2017-2018 Microsoft Corporation.
- Native notices: `THIRD-PARTY-NOTICES.txt` from `SkiaSharp.NativeAssets.Win32`, SHA-256 `21504C46C4C58AA64C1055BD2DCBC5F9A136B4B8C412ED3CC6740E22C5B127F5`. It covers native components including ANGLE, HarfBuzz, Skia, libpng, FreeType, ICU, libjpeg-turbo, WebP, and zlib.

See `LICENSES/MIT-SkiaSharp.txt` for the full MIT terms and `LICENSES/SkiaSharp.NativeAssets.Win32-THIRD-PARTY-NOTICES.txt` for the complete native notices.

### SQLite

`Microsoft.Data.Sqlite 10.0.10` and `Microsoft.Data.Sqlite.Core 10.0.10` use the MIT License. Copyright © Microsoft Corporation. All rights reserved.

`SQLitePCLRaw.bundle_e_sqlite3`, `SQLitePCLRaw.core`, `SQLitePCLRaw.provider.e_sqlite3`, and `SQLitePCLRaw.lib.e_sqlite3` are version 2.1.12 and use the Apache License 2.0. Copyright 2014-2024 SourceGear, LLC.

See `LICENSES/Apache-2.0.txt` for the full Apache License 2.0, including the NOTICE preservation requirement in Section 4(d). Those four SQLitePCLRaw NuGet packages contain no separate NOTICE file. The native SQLite library distributed by `SQLitePCLRaw.lib.e_sqlite3` is in the public domain; its dedication and blessing are preserved in `LICENSES/SQLite-Public-Domain.txt`.

### Microsoft.Extensions 10.0.10

The following 27 packages use the MIT License. Copyright © Microsoft Corporation. All rights reserved.

- Configuration: `Microsoft.Extensions.Configuration`, `.Abstractions`, `.Binder`, `.CommandLine`, `.EnvironmentVariables`, `.FileExtensions`, `.Json`, `.UserSecrets`
- Dependency injection: `Microsoft.Extensions.DependencyInjection`, `.Abstractions`
- Diagnostics: `Microsoft.Extensions.Diagnostics`, `.Abstractions`
- File providers: `Microsoft.Extensions.FileProviders.Abstractions`, `.Physical`, `Microsoft.Extensions.FileSystemGlobbing`
- Hosting: `Microsoft.Extensions.Hosting`, `.Abstractions`
- Logging: `Microsoft.Extensions.Logging`, `.Abstractions`, `.Configuration`, `.Console`, `.Debug`, `.EventLog`, `.EventSource`
- Options and primitives: `Microsoft.Extensions.Options`, `.ConfigurationExtensions`, `Microsoft.Extensions.Primitives`

These packages carry the same `THIRD-PARTY-NOTICES.TXT`, SHA-256 `6D15E10A101C6BFFF2AB4429ED061BF76C456FC4B23AD6B03E0D0F8377148A21`.

See `LICENSES/MIT-Microsoft.NET.txt` for the full MIT terms and `LICENSES/Microsoft.NET-THIRD-PARTY-NOTICES.txt` for the complete notices.

### OpenTK, GLWpfControl, and GLFW

Introduced transitively by the SkiaSharp WPF view:

- `OpenTK`, `OpenTK.Compute`, `OpenTK.Core`, `OpenTK.Graphics`, `OpenTK.Input`, `OpenTK.Mathematics`, `OpenTK.OpenAL`, `OpenTK.Windowing.Common`, `OpenTK.Windowing.Desktop`, and `OpenTK.Windowing.GraphicsLibraryFramework`, version 4.3.0. The OpenTK 4.3.0 meta-package declares the MIT License. Copyright (c) 2006 - 2020 Stefanos Apostolopoulos for the Open Toolkit library.
- `OpenTK.GLWpfControl 4.2.3`. Its NuGet metadata points to the project's `LICENSE.md`. Copyright (c) 2022 Team OpenTK.
- `OpenTK.redist.glfw 3.3.0-pre20200830200122`. Its `COPYING.md` contains a zlib-style license, SHA-256 `8EA14FDC7EFEE7FE53C79101B97049BD547DC6686CFA05DF4F0686146A561423`. Copyright (c) 2002-2006 Marcus Geelnard and Copyright (c) 2006-2016 Camilla Löwy.

See `LICENSES/MIT-OpenTK.md`, `LICENSES/OpenTK-THIRD_PARTIES.md`, `LICENSES/MIT-GLWpfControl.md`, and `LICENSES/Zlib-GLFW.md` for the complete OpenTK, GLWpfControl, and GLFW terms.

### .NET 10.0.10 self-contained runtime

`runtimepack.Microsoft.NETCore.App.Runtime.win-x64` and `runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64` use the MIT License. Copyright belongs to Microsoft Corporation and .NET Foundation and Contributors, respectively. The .NETCore runtime notices match the Microsoft.Extensions notices described above.

See `LICENSES/MIT-Microsoft.NET.txt`, `LICENSES/MIT-WindowsDesktop.txt`, and `LICENSES/Microsoft.NET-THIRD-PARTY-NOTICES.txt` for the complete terms.

`Microsoft.NET.ILLink.Tasks 10.0.10` is an MIT-licensed build-time tool and is not part of the final runtime dependencies. `runtimepack.Microsoft.Windows.SDK.NET.Ref 10.0.19041.57` is a reference-only package and is not distributed with `Honey.exe`.

The Lucide ISC terms in this file and every license and notice under `LICENSES` are embedded resources in `Honey.exe`.
