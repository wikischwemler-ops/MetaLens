# Prompt für die spätere Umsetzung von MetaLens

Du hilfst mir, **MetaLens** zu entwickeln: ein Tool, das jede auswählbare Datei öffnen und verfügbare allgemeine Datei-Informationen anzeigen kann. Bei unterstützten Formaten soll es zusätzlich Metadaten anzeigen und sicher bearbeiten können.

## Anforderungen

- Unterscheide klar zwischen Dateisystem-Informationen und Metadaten innerhalb des Dateiformats.
- Ermögliche bei jeder Datei mindestens die Anzeige aller verfügbaren allgemeinen Informationen, zum Beispiel Name, Pfad, Größe, Zeitstempel und Attribute, sofern Betriebssystem und Berechtigungen diese liefern.
- Ergänze formatspezifische Metadaten über klar abgegrenzte Provider. Erfinde keine Unterstützung: Nicht erkannte oder nicht sicher bearbeitbare Formate müssen als solche ausgewiesen werden.
- Verändere Dateien nie ohne ausdrückliche Speicherbestätigung.
- Zeige vor dem Speichern die geplanten Änderungen. Bewahre unbekannte Dateiinhalte und Metadaten nach Möglichkeit; wenn das nicht sicher gewährleistet werden kann, warne oder verweigere die Änderung.
- Behandle Lese-, Validierungs- und Speicherfehler ausdrücklich. Ein fehlgeschlagener Speichervorgang darf die Quelldatei nicht beschädigen.
- Verarbeite Dateien standardmäßig lokal und führe keine eingebetteten Inhalte aus.

## Arbeitsweise

1. Untersuche zuerst das vorhandene Projekt, seine Konventionen, Abhängigkeiten und Tests.
2. Stelle die noch offenen Produktentscheidungen nicht als bereits beschlossen dar. Insbesondere sind Zielplattform, Technologie und genaue Formate für die erste Version noch offen.
3. Wenn eine dieser Entscheidungen für die Umsetzung zwingend nötig ist, frage zuerst gezielt nach. Ansonsten mache einen kleinen, rückgängig zu machenden Vorschlag und kennzeichne die Annahme.
4. Implementiere in kleinen, zusammenhängenden Änderungen und integriere die Lösung in die vorhandene Architektur.
5. Ergänze passende Tests für allgemeine Datei-Informationen, Format-Erkennung, Metadatenbearbeitung und fehlgeschlagene Speichervorgänge.
6. Führe die kleinsten passenden Tests, Linter oder Builds aus und berichte genau, was erfolgreich war und was nicht.
7. Dokumentiere unterstützte Formate, Einschränkungen und Sicherheitsverhalten.

Beginne mit der Bestandsaufnahme des Projekts und schlage anschließend den nächsten konkreten Umsetzungsschritt vor. Ändere keine Dateien, bevor die erforderlichen offenen Produktentscheidungen geklärt sind.
