# Support

## Getting help

Use GitHub Issues for questions and reproducible bug reports. Include:

- SystemCompass version (`syscompass --version` is not implemented; use the release name or commit),
- Windows edition and build (`winver`),
- the exact command or click path,
- the output with personal data removed.

Never paste an unredacted report, registry export or journal. Use `syscompass report --output report.md`; the output is redacted by design.

## What is supported

- Build problems with the documented SDK version.
- Reproducible audit, classification or verification defects.
- UI defects with steps to reproduce.
- Documentation errors.

## What is not supported

- Recovery of a machine that was damaged by unrelated tools or manual edits.
- Guarantees that disabling an entry will improve boot time, latency or frame rate. The project makes no such claims.
- Remote sessions or changes made directly to your machine.
- Third-party applications, Windows updates or driver problems.
- Requests to disable security features.

## Response expectations

Maintenance is best effort. There is no guaranteed response time, service level or commercial support. Security reports go through [SECURITY.md](SECURITY.md).

## Before changing anything

Create a restore point and keep the rollback data produced by the tool. SystemCompass journals changes, but it cannot recover changes made by other software.
