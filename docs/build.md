# Build and Release

## Prerequisites

- Windows 10 1809 or later (x64 or ARM64)
- .NET SDK **10.0.300** (pinned in `global.json`, `rollForward: latestFeature`)
- No Visual Studio installation is required; the WPF XAML compiler ships with the .NET SDK
- No network access is required at application runtime

## Restore, build, test

```powershell
dotnet restore SystemCompass.slnx
dotnet build SystemCompass.slnx -c Release
dotnet test tests/SystemCompass.Tests/SystemCompass.Tests.csproj -c Release
dotnet format SystemCompass.slnx --verify-no-changes
```

The test host uses Microsoft.Testing.Platform, selected in `global.json`:

```json
{ "test": { "runner": "Microsoft.Testing.Platform" } }
```

Integration tests that enumerate the real machine are read-only. They are skipped on non-Windows hosts.

## Run from source

```powershell
dotnet run --project src/SystemCompass.Cli -c Release -- audit
dotnet run --project src/SystemCompass.App -c Release
```

## Publish

Desktop app (self-contained, no runtime installation required):

```powershell
dotnet publish src/SystemCompass.App -c Release -r win-x64 --self-contained true -o artifacts/win-x64
dotnet publish src/SystemCompass.App -c Release -r win-arm64 --self-contained true -o artifacts/win-arm64
```

CLI:

```powershell
dotnet publish src/SystemCompass.Cli -c Release -r win-x64 --self-contained false -o artifacts/cli-win-x64
```

The app project copies `syscompass.exe` into its output so the elevated helper ships next to the app.

## Artifact contents

```text
SystemCompass.exe          desktop app
syscompass.exe             CLI and elevated helper
*.dll / *.json             runtime and application files
licenses/                  third-party license texts
SHA256SUMS.txt             checksums
*.cdx.json                 CycloneDX SBOM
```

## Release workflow

`.github/workflows/release.yml` runs on a version tag (`v*`):

1. Restore, build and test.
2. Publish self-contained `win-x64` and `win-arm64` builds.
3. Publish the framework-dependent CLI.
4. Generate SHA-256 checksums and a CycloneDX SBOM.
5. Attest build provenance.
6. Create a **draft** GitHub release. Publishing the draft is a manual step.

## Reproducibility

Builds are deterministic (`Deterministic=true`) but not guaranteed to be bit-identical across machines: the .NET SDK, WPF XAML compiler and linker may embed environment-dependent metadata. Verify releases by checksum and provenance attestation rather than by rebuilding byte-for-byte.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `WMC9999` XAML internal error | A C# compile error in the app project masks the real message | Build the app project alone and fix C# errors first |
| `apphost.exe` cannot be copied | The app is still running | Close `SystemCompass.exe` and rebuild |
| `dotnet test` requires the MTP runner | `global.json` missing or overridden | Keep the `test.runner` entry |
| Tests cannot read startup entries | Running under a restricted account | Integration tests are read-only; run them as the interactive user |
| Elevation helper not found | App was published without the CLI | Use the release artifact or publish the CLI into the app folder |
