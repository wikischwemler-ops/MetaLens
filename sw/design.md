# Softwareentwurf: MetaLens

## Produktname

**MetaLens** — ein Dateiinformations- und Metadaten-Editor.

## Leitidee

Die Anwendung behandelt jede Datei zunächst als Datei und ergänzt dieses gemeinsame Fundament durch optionale, formatspezifische Unterstützung. Ein allgemeiner Datei-Inspektor stellt deshalb stets den Einstieg dar; Format-Plugins liefern zusätzliche Informationen und Bearbeitungsmöglichkeiten, wenn sie das Format zuverlässig erkennen.

## Architektur

### 1. Oberfläche

- Datei öffnen und aktuell untersuchte Datei anzeigen.
- Allgemeine Datei-Informationen in einem einheitlichen Bereich zeigen.
- Formatspezifische Metadaten getrennt und nach Gruppen anzeigen.
- Jeden Wert mit Quelle und Status kenntlich machen: lesbar, bearbeitbar, schreibgeschützt oder nicht unterstützt.
- Änderungen zunächst als Entwurf halten und erst nach Bestätigung speichern.
- Vor dem Speichern eine Änderungsvorschau und bei Bedarf eine Sicherungsoption anbieten.

### 2. Datei-Inspektor

Verantwortlich für Dateiname, Pfad, Größe, Erweiterung, Zeitstempel und Attribute. Diese Daten kommen teilweise vom Betriebssystem oder Dateisystem und sind nicht notwendigerweise im Dateiinhalt gespeichert. Nicht verfügbare Werte werden als nicht verfügbar ausgewiesen, nicht geraten.

### 3. Format-Erkennung

Erkennt ein Format nicht allein anhand der Erweiterung, sondern nutzt – soweit möglich – den Dateiinhalt und vorhandene Formatbibliotheken. Unsichere oder widersprüchliche Erkennung wird als unbekannt markiert.

### 4. Metadaten-Provider

Ein Provider implementiert für ein Format:

- Erkennen und Lesen,
- Beschreibung der Felder und ihrer Herkunft,
- Kennzeichnung bearbeitbarer Felder,
- Validierung von Änderungen,
- sicheres Schreiben und Rückmeldung von Fehlern.

Die erste Version sollte mit einem kleinen, bewusst gewählten Satz verbreiteter Formate beginnen. Die konkrete Auswahl hängt von Plattform, Bibliotheken und Produktumfang ab und ist noch festzulegen. Weitere Formate können danach ergänzt werden, ohne die allgemeine Datei-Ansicht zu ändern.

### 5. Speicherablauf

1. Quelldatei öffnen und relevante Dateiinformationen erfassen.
2. Änderungen in einem separaten Entwurf halten.
3. Änderungen gegen die Regeln des zuständigen Providers validieren.
4. Änderungsvorschau anzeigen und Bestätigung abwarten.
5. Soweit praktikabel zunächst in eine temporäre Datei schreiben und prüfen.
6. Quelldatei erst nach erfolgreicher Verarbeitung ersetzen; Sicherung und Wiederherstellung nach der gewählten Plattform-Strategie ermöglichen.
7. Ergebnis und etwaige Einschränkungen ausdrücklich melden.

Ein Provider darf keine unbekannten Dateiinhalte stillschweigend verwerfen. Wenn eine Bibliothek das Erhalten unbekannter Bereiche nicht sicherstellen kann, muss die Anwendung vor dem Speichern warnen oder die Bearbeitung ablehnen.

## Sicherheit und Datenschutz

- Dateien sind nicht vertrauenswürdige Eingaben. Parser müssen Fehler und ungewöhnlich große Eingaben kontrolliert behandeln.
- Keine Ausführung von Dateiinhalt, Makros oder eingebetteten Skripten.
- Keine Netzwerkübertragung ohne explizite, informierte Zustimmung.
- Nur erforderliche Zugriffe und Berechtigungen verwenden.
- Fehlermeldungen dürfen keine vertraulichen Dateiinhalte offenlegen.

## Offene Produktentscheidungen

- Zielplattform und gewünschte Anwendungsform (Desktop, Web oder Kommandozeile).
- Programmiersprache und UI-Framework.
- Formate, die in der ersten Version vollständig lesbar und bearbeitbar sein sollen.
- Standardverhalten für Sicherungskopien und Überschreiben der Quelldatei.
- Ob Dateisystem-Zeitstempel direkt änderbar sein sollen und welche Betriebssystem-Einschränkungen gelten.
