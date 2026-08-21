# Contributing

[简体中文](CONTRIBUTING.md) · **English**

Thank you for helping improve Honey. Before submitting code, open an Issue that describes the problem. Use Discussions for usage questions and early-stage ideas.

## Development environment

You need Windows 11, Git, and the .NET 10 SDK:

```powershell
winget install --id Git.Git -e --source winget
winget install --id Microsoft.DotNet.SDK.10 -e --source winget
git clone https://github.com/vectorxxxx/honey-desktop-companion.git
Set-Location .\honey-desktop-companion
dotnet restore Honey.slnx
dotnet test Honey.slnx -c Release --no-restore
```

Build both Windows x64 single-file variants:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

## Submitting a pull request

1. Create a focused branch from the latest `main`.
2. Keep the change scoped and add tests for behavioral changes.
3. Use a clear Chinese commit message, as required by this repository.
4. Make sure the Release test suite and PowerShell regression scripts pass.
5. Describe the impact, verification, and related Issue in the pull request.

Include screenshots or a short video for interface changes. Include before-and-after measurements and the test environment for performance changes.

## Privacy and assets

- Never commit secrets, tokens, personal paths, logs, databases, or real user data.
- State the source and license for every new image, audio file, font, or model.
- Extracted commercial assets and assets with unclear ownership are not accepted.

By contributing, you agree that your contribution is released under the project's [MIT License](LICENSE).
