# SystemCompass startup audit report

Generated: 2026-10-19 15:07:54 UTC

App version: 0.1.0
OS: Windows 11 Pro (fictional demo build 26100)

## System

| Item | Value |
|---|---|
| OS | Windows 11 Pro (fictional demo build 26100) |
| Power plan | Balanced |
| Game Mode | On |
| Hardware-accelerated GPU scheduling | Available |
| Virtualization-based security | Running |
| Secure Boot | On |

## Startup summary

| Item | Value |
|---|---|
| Entries found | 8 |
| Enabled | 7 |
| Disabled | 1 |
| Protected | 4 |
| Unclear | 1 |

## Privacy

| Item | Value |
|---|---|
| Network requests | None |
| Telemetry | None |
| Export redaction | User names, profile paths, machine names, SIDs, non-system drives |

## Boot measurement

- Last boot event: 2026-10-19 12:07:54
- Boot duration: 18.4 s (single sample, not a benchmark)
- Source: Event 100, Diagnostics-Performance (single sample, not a benchmark)

## Startup entries

| Name | Mechanism | Scope | State | Category | Risk | Signature | Command |
|---|---|---|---|---|---|---|---|
| Contoso Cloud Sync | RunKey | CurrentUser | Enabled | DisableRecommended | Low | Valid | <USERPROFILE>\AppData\Local\Contoso\sync.exe --background |
| Northwind Update Helper | ScheduledTask | AllUsers | Disabled | DisableRecommended | Low | Valid | C:\Program Files\Northwind\updater.exe /silent |
| Windows Security notification icon | RunKey | AllUsers | Enabled | SecurityRelevant | Low | ValidCatalog | C:\Windows\System32\SecurityHealthSystray.exe |
| Remote Support Agent | Service | System | Enabled | SecurityRelevant | Medium | Valid | "C:\Program Files\Example Remote\agent.exe" --service |
| Contoso Audio Service | Service | System | Enabled | SystemCritical | High | Valid | C:\Windows\System32\DriverStore\FileRepository\contoso_audio\ContosoAudio.exe |
| Studio Notes | StartupFolder | CurrentUser | Enabled | Optional | Low | Unsigned | <USERPROFILE>\AppData\Roaming\Studio\notes.exe /minimized |
| Hardware Vendor Driver | Service | System | Enabled | SystemCritical | High | Valid | C:\Windows\System32\drivers\vendor.sys |
| Zoom Meetings | RunOnceKey | CurrentUser | Enabled | Unclear | Medium | Valid | <USERPROFILE>\AppData\Roaming\Zoom\bin\Zoom.exe --autostart |

## Recent changes

| Time | Entry | Action | Status |
|---|---|---|---|
| 2026-10-19 14:55:54 | Northwind Update Helper | Disable | Verified |
| 2026-10-19 15:03:54 | Studio Notes | Enable | Verified |

_Names, user-profile paths, machine names, SIDs and non-system drive paths are redacted._
