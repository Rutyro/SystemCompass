# Roadmap

The roadmap is directional, not a commitment. Priorities favour correctness and reversibility over feature count.

## 0.1.0 — foundation

Shipped in this repository:

- Six startup mechanisms with classification and protected rules
- Verified, journaled, reversible changes
- Snapshots and diff detection
- Curated tweak catalog with evidence levels and deny-list
- Redacted reports, CLI JSON, WPF (AvalonDock) app, demo mode
- CI, security and documentation workflows

## 0.2.0 — depth and confidence

- UI automation tests for the six dockable documents
- Snapshot comparison view in the app, with new/re-enabled highlighting
- Export of a before/after comparison report
- Additional provider tests with captured fixture XML and registry data
- Signed release artifacts and documented verification steps
- Localization scaffolding for the desktop app (English first)

## 0.3.0 — broader scope

- `bcdedit` support with explicit snapshots, reboot-aware verification and a separate high-risk confirmation
- Scheduled-task provider that uses the Task Scheduler COM API as an alternative to `schtasks` parsing
- Store-app startup task inventory
- Import/export of a change set for offline review
- Provider plugin boundary for future mechanisms

## 1.0.0 — stability

- Stable CLI and journal schema with documented compatibility guarantees
- Full German UI translation
- Performance and accessibility review
- Reproducible release verification documentation
- Long-term supported Windows baseline

## Explicit non-goals

- Debloat lists, "FPS packs" or bundled third-party binaries
- Disabling Defender, VBS/HVCI, mitigations or Windows Update
- Automatic, unattended optimization of a user's machine
- Network accounts, cloud sync or telemetry
