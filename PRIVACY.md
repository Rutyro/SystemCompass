# Privacy

SystemCompass is designed to run entirely on the local machine.

## Data stored locally

| Data | Location | Purpose |
|---|---|---|
| Change journal | `%LOCALAPPDATA%\SystemCompass\journal.jsonl` | Verification status and rollback |
| Snapshots | `%LOCALAPPDATA%\SystemCompass\snapshots\` | Detecting new or re-enabled entries |
| Reports | Only where you save them | Redacted Markdown/JSON export |

The journal contains entry names, mechanisms, actions and states. It does **not** contain credentials, tokens, document contents or network data.

## Network and telemetry

- SystemCompass makes no network requests.
- There is no telemetry, analytics, crash reporting or update check.
- `BannedApiAnalyzers` rejects `HttpClient`, `WebRequest`, `Socket` and `Dns` at compile time.
- Authenticode verification is configured for cache-only retrieval with revocation checks disabled, so it does not contact Microsoft or a certificate authority.

## Exports

Every report and export passes through `Redactor`:

- user name → `<USER>`
- user profile path → `<USERPROFILE>`
- machine name → `<MACHINE>`
- security identifiers → `<SID>`
- paths on non-system drives → `<DRIVE>\…`
- additional user-defined prefixes and literals → `<PATH>` / `<REDACTED>`

There is no unredacted export command.

## Demo mode

`--demo` uses fictional entries and a fictional report. It does not read real startup entries.

## Elevation helper

Machine-scope changes write a short-lived intent file to `%TEMP%`. It contains entry ids and requested states only. The file and its result file are deleted after the operation. The elevated helper re-audits the entries and refuses a token that belongs to a different user than the requester.

## Deletion

```powershell
syscompass data export --output report.md
syscompass data delete --yes
```

Deleting the data directory removes the journal and snapshots. Reports you saved elsewhere are not touched.

## Windows and third parties

Windows itself records some actions in the Event Log, Task Scheduler history and service configuration. Those records are outside SystemCompass and are not deleted or modified. Third-party applications may keep their own logs.

## Legal notice

This document describes technical behaviour. It is **not** a certification of GDPR compliance and not legal advice. Because the tool runs locally and the project collects no data, no data is transmitted to the project author. If you deploy the tool in an organisation, you are responsible for evaluating your own legal and organizational requirements.
