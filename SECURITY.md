# Security Policy

## Supported versions

| Version | Supported |
|---|---|
| 0.1.x | Yes, until a newer minor version is released |

The project is pre-1.0. Security fixes target the latest release line.

## Reporting a vulnerability

**Do not open a public issue.** Use GitHub's private vulnerability reporting: open the repository's **Security** tab and choose **Report a vulnerability**. Include:

- affected version or commit,
- Windows build and edition,
- reproduction steps or a proof of concept,
- impact you believe is possible,
- whether the issue requires administrator rights or local access.

There is no security email address and no bug bounty. Reports are handled on a best-effort basis, and you will receive an acknowledgement when the report has been read.

## In scope

- The elevation helper (`apply-intent`) and its trust boundary, including intent-file validation and requester-SID checks.
- Argument handling for `schtasks.exe`, `sc.exe` and `wevtutil.exe` (command injection).
- Redaction defects that allow personal identifiers into an export.
- Authentication or trust decisions in the Authenticode verifier.
- Journal integrity or rollback behaviour that could restore an unintended state.
- Any path that changes system state without the explicit user action.

## Out of scope

- Vulnerabilities in Windows itself or in third-party applications that SystemCompass displays.
- Effects that require administrator rights and are the documented purpose of the tool.
- Missing hardening in unrelated Windows features.
- Social engineering or physical access.
- Issues in builds you produced yourself from modified sources.

## Safe harbour

Good-faith research on your own systems is welcome. Do not test against systems you do not own or have explicit permission to test. The project cannot authorize access to third-party systems.

## Disclosure

Please allow time for a fix before public disclosure. Once a fix is released, the report may be credited if you wish; no personal details are published without consent.
