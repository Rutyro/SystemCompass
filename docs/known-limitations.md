# Known Limitations

## Scope and accuracy

- The audit covers user-session autostart mechanisms. Kernel drivers, shell extensions, credential providers and Group Policy scripts are out of scope.
- Classification is rule-based. A rule that does not match produces `Unclear`, not a guess.
- Protected rules prefer signer and path, but a renamed or repackaged binary can still be misclassified. Review `Unclear` entries manually.
- Localized service display names can be resource strings. When the display name starts with `@`, the service key name is shown instead.

## Changes

- Services are set to Manual, never Disabled, stopped or deleted. A trigger-start service may still start.
- RunOnce and Winlogon entries cannot be changed.
- A successful write can still be overridden by Group Policy or MDM. The read-back then reports a mismatch, and the change is marked failed.
- The app never runs elevated. Machine-scope changes require an interactive UAC prompt and the elevated helper.
- If the UAC prompt is completed with a different administrator account, the helper refuses the batch.
- Reboot-dependent settings are not applied. `requiresReboot` is informational.

## Journal and rollback

- Rollback restores the state recorded by SystemCompass. It cannot undo changes made by other tools, Windows updates or manual edits.
- The journal is plain local JSONL. It is not signed or encrypted and can be edited by any process with access to the user profile.
- If an entry disappears between change and rollback, the rollback fails rather than guessing.

## Reports and privacy

- Redaction covers the current user name, profile path, machine name, SIDs and non-system drive paths. A custom identifier that does not match those patterns is not detected automatically; use the `ExtraLiterals` option in code or review reports before sharing.
- Boot duration is a single Event 100 sample. It is not a benchmark and does not prove an improvement.
- The report includes entry names. Application names can still reveal which software is installed.

## Platform and build

- Windows 10 1809 or later. Older builds are not tested.
- x64 and ARM64 are the intended targets. No x86 build.
- The desktop app is self-contained and large; the CLI is framework-dependent by default in release packaging.
- Builds are not bit-for-bit reproducible across machines.
- No UI automation tests in 0.1.0.

## Claims deliberately not made

- No FPS, latency, boot-time or stability improvement is claimed.
- No "gaming optimization" effect is claimed.
- No official certification, audit or compliance status is claimed.
