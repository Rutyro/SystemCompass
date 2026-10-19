# Contributing to SystemCompass

Thank you for helping. SystemCompass changes operating-system state, so contributions are held to a higher bar than in a typical desktop tool.

## Ground rules

1. **Clean room.** Do not copy code or scripts from other tuning packs, forums or repositories. Tweak definitions must be derived from documentation or your own testing, with a link for `MicrosoftDocumented` entries.
2. **No security-reducing tweaks.** Defender, VBS/HVCI, exploit mitigations, DEP and Windows Update policies are refused by `SecurityDenyList`; a catalog test enforces this.
3. **No network code.** `HttpClient`, `WebRequest`, `Socket` and `Dns` are banned by the analyzer configuration. Do not work around it.
4. **No deletion of user data or system configuration.** Prefer reversible state changes and capture the previous value.
5. **License compatibility.** New dependencies need an OSI-approved license compatible with MIT. Add them to `Directory.Packages.props` and `THIRD_PARTY_NOTICES.md`.

## Development setup

Requirements: Windows 10 1809+, .NET SDK 10.0.300+.

```powershell
dotnet build SystemCompass.slnx -c Release
dotnet test tests/SystemCompass.Tests/SystemCompass.Tests.csproj -c Release
dotnet format SystemCompass.slnx --verify-no-changes
```

The test suite is Microsoft.Testing.Platform based. Tests that touch the real system must be read-only and tagged `Integration`.

## What to test

- Pure logic belongs in `SystemCompass.Core` and must be unit-tested with fakes (`IRegistry` equivalents, `IStartupProvider`, `ITweakBackend`).
- A change is only complete when the state is written, read back and verified.
- New providers need: enumeration, `GetState`, `SetState`, failure behaviour, and a read-only integration test when practical.
- New tweaks need: evidence level, link (for Microsoft-documented items), risk, OS range and operations. Run `TweakCatalog.Validate()` via the tests.

## Commit and pull request conventions

- Commits are small and describe the change in the imperative mood (`Add scheduled task provider`).
- One topic per pull request. Include tests and documentation updates in the same pull request.
- The pull request checklist in `.github/PULL_REQUEST_TEMPLATE.md` must be completed.
- Do not include generated binaries, local journals, reports, screenshots with personal data or machine-specific identifiers.

## Reporting problems

- Functional bugs and feature requests: GitHub Issues.
- Security vulnerabilities: follow [SECURITY.md](SECURITY.md) and report privately. Never open a public issue for a vulnerability.
