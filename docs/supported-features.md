# Supported and Unsupported Features

## Supported in 0.1.0

| Capability | Details |
|---|---|
| Run keys | HKCU and HKLM, including WOW6432Node |
| RunOnce | Read-only display |
| Startup folders | User and all users, `.lnk` and `.url` |
| StartupApproved | Read and write for Run, Run32 and StartupFolder |
| Scheduled tasks | Boot and logon triggers, all task folders |
| Services | Automatic Win32 services, read and set to Manual/Automatic |
| Winlogon | Shell and Userinit, read-only |
| Signature | Embedded and catalog Authenticode, signer display |
| Classification | Rules with mechanism, name, path, location and signer matching |
| Protected entries | Security, driver, remote-access and explicit product rules |
| Change journal | Append-only JSONL, rollback of verified changes |
| Snapshots | Save and compare; new, removed, re-enabled, changed |
| Tweaks | Registry set/delete, service start type, active power scheme |
| Reports | Redacted Markdown and JSON |
| CLI JSON | Audit, report and tweak listing |
| Demo mode | Fictional entries and reports |
| Elevation | One UAC prompt per batch, requester-SID check |

## Not supported in 0.1.0

| Capability | Reason |
|---|---|
| Deleting Run values or applications | Deliberately avoided; state changes are reversible |
| Stopping, disabling or deleting services | Disabling can break drivers and recovery; Manual is the reversible step |
| `bcdedit` changes | High impact and reboot-dependent; deferred |
| Driver services as startup entries | Drivers are kernel components, not user-session autostart |
| Policy overrides by MDM / Group Policy | A successful write can still be overridden; read-back reports the discrepancy |
| Over-the-shoulder elevation with another admin account | Refused by design; the requester SID must match |
| UI automation tests | Logic is tested; UI automation is on the roadmap |
| Localized UI | English UI in 0.1.0; documentation is English and German |
| Code signing of release binaries | Not available in this repository; verify checksums and provenance |
| Network-based update checks | No network code by design |

## Mechanisms reported but intentionally read-only

- `RunOnce` values
- `Winlogon` Shell and Userinit
- Protected entries of any mechanism

## Mechanisms not inventoried in 0.1.0

- Store-app startup tasks registered through the package model
- Shell extensions and `AppInit_DLLs`
- Print monitors, credential providers and other injection points
- Group Policy logon scripts
- Driver and kernel services

These are out of scope for a startup **application** audit and are not modified.
