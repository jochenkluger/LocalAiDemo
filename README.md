# LocalAiDemo

## Text-Generierungs-Services

Diese Anwendung enthält zwei verschiedene Implementierungen für Text-Generierung:

### 1. LocalTextGenerationService

Dies ist die Standard-Implementierung, die Microsoft.Extensions.AI verwendet. Sie ist besonders gut für einfachere Anwendungsfälle und bietet eine nahtlose Integration mit dem Microsoft Extensions-Ökosystem.

- Verwendet direkt LlamaSharp
- Implementiert eigenes Function-Calling durch Parsing der LLM-Antworten
- Nutzt JSON-Extraktion und Regex-Parsing für Funktionsaufrufe

### 2. SemanticKernelTextGenerationService

Dies ist eine alternative Implementierung, die Microsoft.SemanticKernel verwendet. Diese Implementierung bietet:

- Integration mit dem semantischen Kernel-Framework
- Erweiterte Funktionen wie Plugins, Planungsstrategien und semantische Funktionen
- Längerfristige Unterstützung für LLM-Funktionen
- Native Function-Calling-Unterstützung durch SemanticKernel

## Umschalten zwischen den Services

Um zwischen den Services zu wechseln, kann die `appsettings.json` bearbeitet werden:

```json
{
  "AppSettings": {
    "AI": {
      "UseSemanticKernel": true  // Auf true setzen, um SemanticKernel zu verwenden
    }
  }
}
```

## Implementierungsdetails

Beide Services implementieren das `ITextGenerationService`-Interface und verhalten sich aus Anwendungssicht identisch. Der Hauptunterschied liegt in der zugrunde liegenden Technologie:

- `LocalTextGenerationService`: Verwendet direkt LlamaSharp mit Microsoft.Extensions.AI
- `SemanticKernelTextGenerationService`: Verwendet LlamaSharp über eine SemanticKernel-Integration

## Function-Calling

Beide Implementierungen unterstützen Function-Calling, um die `CreateMessage`-Funktion aufzurufen:

### LocalTextGenerationService (manuelles Function-Calling)

Im LocalTextGenerationService wird Function-Calling manuell implementiert:

1. Die Systemnachricht wird mit Funktionsbeschreibungen erweitert
2. Das LLM wird instruiert, JSON-Antworten für Funktionsaufrufe zu generieren
3. Die Antwort wird mit Regex analysiert, um JSON-Blöcke zu extrahieren
4. Funktionsaufrufe werden manuell verarbeitet und an den `MessageCreator` weitergeleitet

### SemanticKernelTextGenerationService (natives Function-Calling)

SemanticKernel bietet native Unterstützung für Function-Calling:

1. Ein `IMessageCreator` wird als Kernel-Plugin registriert
2. Die `CreateMessage`-Funktion wird automatisch erkannt und als Tool bereitgestellt
3. SemanticKernel übernimmt die Extraktion und Ausführung von Funktionsaufrufen
4. Die Ergebnisse werden automatisch in den Chat-Kontext zurückgeführt

## Weitere Entwicklungsmöglichkeiten

Die Services können erweitert werden mit:

1. **Weitere Funktionen**: Implementierung zusätzlicher Funktionen für das LLM
2. **Komplexe Plugins**: Entwicklung von Plugins für erweiterte Anwendungsfälle
3. **Verteilte Ausführung**: Verteilung von Aufgaben auf mehrere Modelle
4. **Planungsstrategien**: Implementierung komplexer Aufgaben durch Zerlegung in Teilschritte

## Beispiel-Verwendung

```csharp
// Zugriff auf den Service über die Dependency Injection
@inject ITextGenerationService TextGenerationService

// Initialisierung
await TextGenerationService.InitializeAsync(progress => {
    // Fortschritt anzeigen
});

await TextGenerationService.StartChatAsync();

// Nachricht an das Modell senden
var antwort = await TextGenerationService.InferAsync("Wie ist das Wetter heute?");

// Nachricht an das Modell senden, die einen Funktionsaufruf auslösen könnte
var nachricht = await TextGenerationService.InferAsync(
    "Erstelle eine Nachricht an Paul mit dem Text 'Hallo, wie geht es dir?'");

// Das Modell wird die CreateMessage-Funktion aufrufen, wenn es die Absicht erkennt
```