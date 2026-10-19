# Changelog

All notable changes to this project are documented in this file.

The format follows Keep a Changelog, and the project adheres to Semantic Versioning.

## [Unreleased]

### Added

- WPF desktop shell built on AvalonDock 5 and the Ms-PL Extended WPF Toolkit fork: six dockable documents, saved layouts, dark theme, dependency-free charts, report notes/print, guided-cleanup wizard and demo screenshots.
- `tcpGlobal` operation for allow-listed `netsh int tcp set global` values.
- `scheduledTask` operation for deny-listed scheduled-task state changes.
- 32 additional curated tweaks migrated from the author's own scripts, including search/privacy policies, diagnostic tasks, TCP/AFD settings, power timers and per-effect visual overrides. Catalog total: 64.
- Tests for TCP and scheduled-task round trips and for the extended deny-list.

### Changed

- The WinUI 3 shell was replaced by WPF; Windows App SDK and MSIX build dependencies were removed.
- The security deny-list now also rejects UAC, SmartScreen, phishing-filter, telemetry-reporting and network-hardening values.

## [0.1.0] - 2026-10-19

### Added

- Startup audit for Run/RunOnce keys, Startup folders, StartupApproved flags, scheduled tasks with boot/logon triggers, automatic Win32 services and Winlogon entries.
- Rule-based classification with six categories, risk level, signer information, reason and recovery advice.
- Protected-entry rules for Windows security components, driver services, remote-access tooling and TeamViewer.
- Reversible state changes through StartupApproved flags, `schtasks /Change` and `sc config`, with read-back verification.
- Append-only JSONL change journal with rollback of verified changes.
- Startup snapshots and diff detection for new, removed, re-enabled and changed entries.
- Curated tweak catalog with evidence levels, OS build ranges, captured previous values and revert.
- Security deny-list that refuses Defender, VBS/HVCI, exploit mitigation, DEP and Windows Update policy changes.
- Redacted Markdown and JSON reports with diagnostics and a single boot-duration sample.
- WinUI 3 desktop app with startup, control center, report and journal pages.
- CLI (`syscompass`) with audit, report, disable, enable, rollback, tweak and data commands.
- Elevated helper with intent-file validation and requester-SID check for machine-scope changes.
- Demo mode with fictional entries and reports for screenshots and evaluation.
- Unit tests for startup approval flags, command-line parsing, classification, journaling, rollback, tweak apply/revert, redaction and report output.
- CI, security, documentation and draft-release workflows.

### Security

- Reject UNC paths, device paths, mapped network drives and link targets before signature, icon or shell probes, preventing SMB authentication from startup entries.
- Enforce the protected-entry rule in `ChangeExecutor`, so the CLI, rollback and the elevated helper refuse them too.
- Redact entry names, signer names, journal errors and reviewer notes in reports, not only command lines and paths.
- Fix the Defender policy deny-list fragment and cover the correct key in tests.
- Make demo mode refuse apply and revert in the Control Center.
- Require the elevated intent file and data directory to be local, reject reparse intent files, keep the intent handle open across the UAC prompt and create the result file with `CreateNew`. Malformed JSON is reported cleanly.
- No network code; banned-API analysis rejects HTTP, socket, DNS and web-request types at compile time.
- Authenticode verification uses cache-only retrieval with revocation checks disabled.
- Exports are always redacted; there is no unredacted export path.
- Machine-scope changes require explicit confirmation and a UAC prompt.

### Known limitations

- RunOnce and Winlogon entries are read-only.
- Services are set to Manual, not stopped or deleted.
- Boot duration is a single Event 100 sample, not a benchmark.
- Machine-scope changes require interactive elevation.
- No UI automation tests in this release.
