# Chat Vectorization Services

Diese Implementierung erweitert die LocalAiDemo-Anwendung um umfassende Chat-Vektorisierungsfunktionen mit SQLite Vector Extension.

## Übersicht der Services

### 1. ChatVectorizationService
**Haupt-Service für Batch-Vektorisierung und grundlegende Operationen**

#### Funktionen:
- `VectorizeUnprocessedChatsAsync()` - Vektorisiert alle Chats ohne Embeddings
- `VectorizeUnprocessedMessagesAsync()` - Vektorisiert alle Nachrichten ohne Embeddings
- `ReVectorizeAllChatsAsync()` - Re-vektorisiert alle Chats (z.B. bei Model-Wechsel)
- `ReVectorizeAllMessagesAsync()` - Re-vektorisiert alle Nachrichten
- `FindSimilarMessagesAsync()` - Findet ähnliche Nachrichten global
- `FindSimilarMessagesInChatAsync()` - Findet ähnliche Nachrichten in einem Chat
- `GetVectorizationStatsAsync()` - Statistiken über Vektorisierungsstatus
- `UpdateChatVectorAsync()` / `UpdateMessageVectorAsync()` - Einzelne Updates

### 2. AdvancedVectorService
**Erweiterte Vector-Operationen und Analytics**

#### Funktionen:
- `HybridSearchAsync()` - Kombiniert Vector- und Text-Suche
- `GetChatSimilarityStatsAsync()` - Detaillierte Ähnlichkeitsstatistiken für einen Chat
- `FindChatClustersAsync()` - Findet Cluster ähnlicher Chats
- `RebuildVectorIndicesAsync()` - Rebuilding der Vector-Indices
- `GetVectorDatabaseStatsAsync()` - Umfassende Datenbankstatistiken
- `CleanupOrphanedVectorEntriesAsync()` - Aufräumen verwaister Vector-Einträge

### 3. ChatVectorService (Facade)
**Einfache API für alle Vector-Operationen**

#### Hauptmethoden:
- `VectorizeAllUnprocessedAsync()` - Komplette Vektorisierung aller unverarbeiteten Inhalte
- `ReVectorizeAllAsync()` - Komplette Re-Vektorisierung
- `HybridSearchAsync()` - Intelligente Suche
- `CleanupAsync()` - Komplett-Cleanup
- `GetStatsAsync()` - Schnelle Statistiken

## Verwendung

### Dependency Injection
```csharp
// In MauiProgram.cs bereits registriert:
builder.Services.AddSingleton<IChatVectorizationService, ChatVectorizationService>();
builder.Services.AddSingleton<IAdvancedVectorService, AdvancedVectorService>();
builder.Services.AddSingleton<IChatVectorService, ChatVectorService>();
```

### Beispiel: Basis-Vektorisierung
```csharp
@inject IChatVectorService ChatVectorService

// Alle unverarbeiteten Chats und Nachrichten vektorisieren
var result = await ChatVectorService.VectorizeAllUnprocessedAsync();
if (result.Success)
{
    Console.WriteLine($"Processed {result.ChatsProcessed} chats and {result.MessagesProcessed} messages in {result.Duration}");
}
```

### Beispiel: Hybrid-Suche
```csharp
// Suche mit Kombination aus Vector- und Text-Matching
var results = await ChatVectorService.HybridSearchAsync("KI Entwicklung", 10);
foreach (var result in results)
{
    Console.WriteLine($"Chat: {result.Chat.Title}, Score: {result.HybridScore:F3}");
}
```

### Beispiel: Chat-Clustering
```csharp
@inject IAdvancedVectorService AdvancedVectorService

// Finde Gruppen ähnlicher Chats
var clusters = await AdvancedVectorService.FindChatClustersAsync(5);
foreach (var cluster in clusters)
{
    Console.WriteLine($"Cluster {cluster.Id}: {cluster.Chats.Count} chats, avg similarity: {cluster.AverageSimilarity:F3}");
}
```

## Admin-Interface

Die Anwendung enthält eine neue **Vector Admin**-Seite (`/vector-admin`), die folgende Funktionen bietet:

### Dashboard
- **Statistiken**: Zeigt Vektorisierungsstatus für Chats und Nachrichten
- **Operationen**: Ein-Klick-Buttons für alle Haupt-Operationen
- **Ergebnisse**: Detaillierte Anzeige der letzten Operation
- **Suche**: Live-Test der Hybrid-Suchfunktion

### Verfügbare Operationen
1. **Vectorize Unprocessed** - Vektorisiert nur neue/unverarbeitete Inhalte
2. **Re-vectorize All** - Komplette Re-Vektorisierung (bei Model-Änderungen)
3. **Cleanup Orphaned Vectors** - Entfernt verwaiste Vector-Einträge
4. **Rebuild Vector Indices** - Neuaufbau der SQLite Vector-Indices

## Technische Details

### Datenbank-Schema
Die bestehenden Tabellen wurden erweitert:

```sql
-- Chat Tabelle (bereits vorhanden)
CREATE TABLE Chat (
    Id INTEGER PRIMARY KEY,
    Title TEXT,
    CreatedAt TEXT,
    PersonId INTEGER,
    IsActive INTEGER,
    EmbeddingVector BLOB  -- Neu: Serialisierte float[] Vektoren
);

-- ChatMessage Tabelle (bereits vorhanden)
CREATE TABLE ChatMessage (
    Id INTEGER PRIMARY KEY,
    ChatId INTEGER,
    Content TEXT,
    Timestamp TEXT,
    IsUser INTEGER,
    EmbeddingVector BLOB  -- Neu: Serialisierte float[] Vektoren
);

-- Vector Search Tabelle (SQLite Vector Extension)
CREATE VIRTUAL TABLE chat_vectors USING vec0(
    embedding_vector FLOAT[128],
    chat_id INTEGER UNINDEXED
);
```

### Vector-Generierung
- **Chat-Vektoren**: Kombiniert aus Titel + ersten 5 Nachrichten
- **Message-Vektoren**: Direkt aus Nachrichteninhalt
- **Dimension**: 128 (konfigurierbar im EmbeddingService)
- **Normalisierung**: Alle Vektoren sind L2-normalisiert

### Performance-Optimierungen
- **Batch-Verarbeitung**: Verarbeitet Inhalte in Batches
- **Lazy Loading**: Generiert Vektoren nur bei Bedarf
- **Caching**: Nutzt bestehende Vektoren wenn verfügbar
- **Background Tasks**: Lange Operationen laufen im Hintergrund

## Automatische Vektorisierung

### Bei neuen Chats
Beim Speichern eines neuen Chats (`ChatDatabaseService.SaveChatAsync`):
1. Automatische Vector-Generierung wenn `EmbeddingVector == null`
2. Speicherung in Haupt-Tabelle und Vector-Search-Tabelle
3. Fehlerbehandlung ohne Breaking der Chat-Speicherung

### Bei neuen Nachrichten
Beim Speichern neuer Nachrichten (`SaveChatMessageAsync`):
1. Automatische Vector-Generierung für nicht-leere Inhalte
2. Nur wenn Vector Search verfügbar ist
3. Graceful Fallback wenn Vektorisierung fehlschlägt

## Monitoring & Wartung

### Statistiken
- **Vektorisierungsgrad**: Prozentsatz vektorisierter Chats/Nachrichten
- **Speicherverbrauch**: Geschätzte Vector-Speichernutzung
- **Performance-Metriken**: Verarbeitungszeiten und Durchsatz

### Wartungsaufgaben
- **Regelmäßige Cleanup**: Entfernung verwaister Vektoren
- **Index-Optimierung**: Neuaufbau der Vector-Indices bei Bedarf
- **Konsistenz-Checks**: Prüfung auf fehlende oder beschädigte Vektoren

## Fehlerbehandlung

### Graceful Degradation
- **Vector Search nicht verfügbar**: Fallback auf manuelle Similarity-Berechnung
- **Embedding-Fehler**: Chat/Message wird ohne Vector gespeichert
- **Batch-Fehler**: Einzelne Fehler brechen nicht die gesamte Operation

### Logging
Umfassendes Logging auf verschiedenen Leveln:
- **Debug**: Detaillierte Verarbeitungsschritte
- **Info**: Operationsstatus und Statistiken
- **Warning**: Fallback-Situationen
- **Error**: Echte Fehler mit vollständigen Traces

## Erweiterungsmöglichkeiten

### Zukünftige Features
1. **Real-time Vectorization**: WebSocket-basierte Live-Updates
2. **Vector Compression**: Reduzierung der Storage-Größe
3. **Custom Embedding Models**: Integration echter AI-Modelle
4. **Advanced Analytics**: ML-basierte Chat-Insights
5. **Export/Import**: Vector-Daten Migration

### Integration Points
- **EmbeddingService**: Einfacher Austausch des Embedding-Algorithmus
- **ChatDatabaseService**: Erweiterte Vector-Storage-Optionen
- **UI Components**: Weitere Admin- und User-Interfaces

## Troubleshooting

### Häufige Probleme

#### "Vector search not available"
- SQLite Vector Extension nicht geladen
- Prüfe `SqliteVectorSearchService.EnableVectorSearchAsync()`
- Stelle sicher, dass `vec0.dll/so/dylib` verfügbar ist

#### "No vectors generated"
- EmbeddingService nicht registriert
- Leere Chat-Inhalte
- Überprüfe Logs für Embedding-Fehler

#### "Performance issues"
- Zu viele Vektoren für manuelle Berechnung
- Nutze Vector Search Table wenn möglich
- Erwäge Batch-Size Reduzierung

### Debug-Tipps
1. **Überprüfe Service-Registrierung** in `MauiProgram.cs`
2. **Monitor Logs** für detaillierte Fehlerinformationen
3. **Teste Vector Admin UI** für schnelle Diagnose
4. **Verwende Stats API** für Performance-Monitoring
