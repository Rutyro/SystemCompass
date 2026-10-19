# Haftungs- und Risikohinweis (Deutsch)

_Dieses Dokument ist allgemeine Projektinformation und keine individuelle Rechtsberatung._

## Zweck

SystemCompass ist ein Diagnose- und Verwaltungswerkzeug für Windows-Autostart-Einträge, Dienste und ausgewählte Betriebssystemeinstellungen. Es richtet sich an technisch erfahrene Benutzer und ist für Systeme bestimmt, die sie besitzen oder verwalten dürfen.

## Risiken

Änderungen an Registry-Werten, Dienststarttypen, geplanten Aufgaben und Energieeinstellungen können:

- Anwendungen oder Windows-Komponenten am Starten hindern,
- zu Daten- oder Konfigurationsverlust führen,
- das System instabil oder nicht startfähig machen,
- die Kompatibilität mit Sicherheitssoftware, Updates oder Treibern beeinträchtigen,
- bei unsachgemäßer Nutzung die Sicherheit verringern.

Das Projekt lehnt Einstellungen, die die Windows-Sicherheit schwächen, bewusst ab. Es kann jedoch nicht verhindern, dass ein Benutzer mit anderen Werkzeugen unsichere Änderungen vornimmt.

## Sicherung und Wiederherstellung

Erstelle vor Änderungen auf Maschinenebene einen Wiederherstellungspunkt und bewahre die vom Werkzeug erzeugten Rollback-Daten auf. Das Journal unterstützt das Zurücknehmen von Änderungen, die SystemCompass selbst vorgenommen hat. Es stellt keine Änderungen wieder her, die durch andere Software, Windows-Updates oder manuelle Eingriffe entstanden sind.

## Keine Leistungsgarantie

SystemCompass garantiert keine Verbesserung von Startzeit, Latenz, Bildrate, Stabilität oder Energieverbrauch. Boot-Messwerte stammen aus einer einzelnen Event-100-Messung und sind kein Benchmark. Eine kürzere Autostart-Liste ist kein Nachweis eines schnelleren Systems.

## Verantwortung bei administrativen Aktionen

Du bist verantwortlich für die von dir bestätigten Aktionen, für erteilte Berechtigungen und für die Eignung des Werkzeugs in deiner Umgebung. Administratorrechte sollten nur erteilt werden, wenn eine Änderung auf Maschinenebene sie erfordert.

## Gewährleistung und Haftung

Die Software wird unter der MIT-Lizenz „wie besehen“ ohne Gewährleistung bereitgestellt. Soweit gesetzlich zulässig, haftet der Autor nicht für mittelbare Schäden, Begleitschäden oder Folgeschäden.

Dieser Hinweis schließt keine Haftung aus, die nach anwendbarem Recht nicht ausgeschlossen werden kann. Insbesondere bleibt die Haftung für Vorsatz, grobe Fahrlässigkeit, die Verletzung von Leben, Körper oder Gesundheit sowie die Haftung nach dem Produkthaftungsgesetz unberührt. Zwingende gesetzliche Verbraucherrechte werden nicht eingeschränkt.

## Drittanbieterkomponenten

SystemCompass verwendet Bibliotheken und Laufzeitkomponenten Dritter. Lizenzen und Hinweise stehen in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Windows und weitere Produktnamen sind Marken ihrer jeweiligen Inhaber.

## Keine Verbindung zu Microsoft

SystemCompass ist ein unabhängiges Projekt. Es ist nicht mit der Microsoft Corporation oder anderen Herstellern, deren Software geprüft wird, verbunden, von ihnen gesponsert oder empfohlen.

## Sicherheitsmeldungen

Melde Sicherheitsprobleme vertraulich gemäß [SECURITY.md](SECURITY.md). Veröffentliche Schwachstellen nicht, bevor eine Korrektur verfügbar ist.

## Externe Links

Links zu Microsoft-Dokumentation und Websites Dritter dienen der Orientierung. Das Projekt kontrolliert deren Inhalte nicht und macht sie sich nicht zu eigen.
