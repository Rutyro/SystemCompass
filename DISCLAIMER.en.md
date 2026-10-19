# Disclaimer and Risk Notice (English)

_This document is general project information. It is not legal advice._

## Purpose

SystemCompass is a diagnostic and management tool for Windows startup entries, services and selected operating-system settings. It is provided for use by technically experienced users on systems they own or are authorised to administer.

## Risks

Changing registry values, service start types, scheduled tasks and power settings can:

- prevent applications or Windows components from starting,
- cause data loss or configuration loss,
- make the system unstable or unbootable,
- break compatibility with security software, updates or drivers,
- reduce security if misused.

The project deliberately refuses settings that weaken Windows security, but it cannot prevent a user from making unsafe changes with other tools.

## Backups and recovery

Create a restore point and keep the rollback data produced by the tool before changing machine-scope settings. The journal supports rollback of changes made by SystemCompass only; it does not restore changes made by other software, by Windows updates or by manual edits.

## No performance guarantee

SystemCompass does not guarantee any improvement in boot time, latency, frame rate, stability or power consumption. Boot measurements come from a single Windows Event 100 sample and are not a benchmark. A shorter startup list is not proof of a faster system.

## Administrative responsibility

You are responsible for the actions you confirm, for the privileges you grant and for the suitability of the tool for your environment. Run the tool with administrator rights only when a machine-scope change requires it.

## Warranty and liability

The software is provided under the MIT License, "as is", without warranty of any kind. To the extent permitted by applicable law, the author is not liable for indirect, incidental or consequential damages.

Nothing in this notice excludes or limits liability that cannot be excluded or limited under applicable law. In particular, liability for intent, gross negligence, injury to life, body or health, and liability under the German Product Liability Act (Produkthaftungsgesetz) remains unaffected. Mandatory statutory rights of consumers are not restricted.

## Third-party components

SystemCompass uses third-party libraries and runtime components. Their licences and notices are listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Windows and other product names are trademarks of their respective owners.

## No affiliation

SystemCompass is an independent project. It is not affiliated with, endorsed by, sponsored by or approved by Microsoft Corporation or any other vendor whose software it inspects.

## Security reports

Report security issues privately as described in [SECURITY.md](SECURITY.md). Do not disclose vulnerabilities publicly before a fix is available.

## External links

Links to Microsoft documentation and third-party sites are provided for orientation. The project does not control their content and does not adopt it as its own.
