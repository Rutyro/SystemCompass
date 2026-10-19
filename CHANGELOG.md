# Changelog

All notable changes to this project are documented in this file.

The format follows Keep a Changelog, and the project adheres to Semantic Versioning.

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
