# Anforderungen: MetaLens

## Ziel

MetaLens ist ein Dateiinformations-Tool, das Dateien unabhängig von ihrem Format öffnen und die verfügbaren Informationen anzeigen kann. Erkannte Metadaten sollen bearbeitbar sein, soweit das Dateiformat und die Zugriffsrechte dies sicher unterstützen.

## Grundsatz

„Jede Datei“ bedeutet: Jede Datei kann ausgewählt und als Datei untersucht werden. Es bedeutet nicht, dass jedes Format dieselben Metadaten besitzt oder dass beliebige unbekannte Inhalte sicher bearbeitet werden können. Die Anwendung muss klar unterscheiden zwischen:

- allgemeinen Dateisystem-Informationen,
- erkannten formatspezifischen Metadaten,
- nicht unterstützten oder nicht zuverlässig interpretierbaren Daten.

## Funktionale Anforderungen

1. Dateien über einen Dateiauswahldialog öffnen können.
2. Für jede ausgewählte Datei allgemeine Informationen anzeigen, sofern verfügbar:
   - Name, Pfad und Erweiterung,
   - Größe und erkannter Medientyp,
   - Erstellungs-, Änderungs- und letzter Zugriffszeitpunkt, soweit Betriebssystem und Dateisystem sie bereitstellen,
   - Dateiattribute und Zugriffsrechte, soweit verfügbar.
3. Das Dateiformat erkennen und passende Metadaten-Leser verwenden.
4. Erkannte formatspezifische Metadaten anzeigen, beispielsweise EXIF/IPTC/XMP bei Bildern und Dokumenteigenschaften bei unterstützten Dokumentformaten.
5. Unterstützte Metadaten bearbeiten und Änderungen ausdrücklich speichern lassen.
6. Nicht unterstützte oder schreibgeschützte Einträge als solche kennzeichnen, statt eine erfolgreiche Bearbeitung vorzutäuschen.
7. Fehler beim Lesen und Speichern verständlich anzeigen; die Originaldatei bei fehlgeschlagenen Änderungen unverändert lassen.
8. Vor dem Speichern eine Zusammenfassung der Änderungen anzeigen.
9. Große Dateien und Dateien mit fehlenden oder fehlerhaften Metadaten kontrolliert behandeln.

## Qualitätsanforderungen

- Keine Datei ohne ausdrückliche Nutzeraktion verändern.
- Originalinhalte und unbekannte Metadaten nach Möglichkeit erhalten.
- Änderungen möglichst atomar speichern; bei unterstützten Formaten vorher eine Sicherung ermöglichen.
- Nicht erkannte Metadaten nicht als vollständig ausschließen: Die Oberfläche weist darauf hin, dass nicht alle proprietären oder formatspezifischen Informationen bekannt sein müssen.
- Dateiinhalte standardmäßig lokal verarbeiten und nicht ohne Zustimmung übertragen.
- Informationen zu Berechtigungen und Dateisystem-Einschränkungen transparent darstellen.

## Nicht-Ziele der ersten Version

- Garantie, jedes proprietäre Format vollständig zu verstehen oder zu bearbeiten.
- Beliebiges Ändern von Bytes unbekannter Formate.
- Umgehen von Betriebssystem-Berechtigungen oder Dateischutz.
- Festlegung auf ein bestimmtes Betriebssystem, UI-Framework oder Programmiersprache; diese Entscheidungen sind noch offen.

## Erfolgskriterien

- Eine beliebige auswählbare Datei kann geöffnet werden und ihre verfügbaren allgemeinen Informationen werden angezeigt.
- Für unterstützte Formate werden passende Metadaten angezeigt und bearbeitbare Werte können gespeichert werden.
- Für nicht unterstützte Formate wird verständlich erklärt, welche Informationen verfügbar sind und warum keine formatspezifische Bearbeitung angeboten wird.
- Ein fehlgeschlagener Speichervorgang beschädigt die Quelldatei nicht.
