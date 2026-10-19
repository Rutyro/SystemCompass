# Summary

Describe what changes and why.

## Type of change

- [ ] Bug fix
- [ ] New provider or mechanism
- [ ] Classification rule
- [ ] Tweak catalog entry
- [ ] Report or privacy
- [ ] UI
- [ ] Documentation
- [ ] Build, CI or dependencies

## Verification

- [ ] `dotnet build SystemCompass.slnx -c Release` passes
- [ ] `dotnet test tests/SystemCompass.Tests/SystemCompass.Tests.csproj -c Release` passes
- [ ] `dotnet format SystemCompass.slnx --verify-no-changes` passes
- [ ] `./scripts/check-clean-room.ps1` passes
- [ ] Read-only integration behaviour was checked on a Windows machine

## Safety checklist

- [ ] No change deletes user data or system configuration without an explicit user action.
- [ ] Every write captures the previous state and verifies the result.
- [ ] Protected entries remain protected.
- [ ] No security-reducing setting was added; the catalog validator passes.
- [ ] No network type was introduced.
- [ ] No personal data, credential, machine name, path or log is included in the diff, tests or screenshots.
- [ ] Screenshots, if any, use demo mode.

## Documentation

- [ ] README or docs updated for user-visible behaviour.
- [ ] CHANGELOG updated.
- [ ] New dependencies added to `Directory.Packages.props` and `THIRD_PARTY_NOTICES.md`.
