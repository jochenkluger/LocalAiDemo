# Thorsten ONNX TTS Setup

Diese Anleitung erklärt, wie Sie das Thorsten ONNX TTS-System in der LocalAiDemo-Anwendung einrichten.

## Was ist Thorsten ONNX TTS?

Thorsten ONNX TTS ist eine fortschrittliche, vollständig lokale Text-zu-Sprache-Engine, die deutsche Sprache mit hoher Qualität synthetisiert. Sie basiert auf neuralen Netzwerken und läuft ohne Internet-Verbindung.

## Voraussetzungen

- Windows 10/11 (empfohlen) oder Linux/macOS
- .NET 8 Runtime
- Mindestens 2 GB freier Festplattenspeicher für Modelle

## Schnelle Einrichtung

### 1. Konfiguration aktivieren

Die Anwendung ist bereits für Thorsten ONNX konfiguriert. In `appsettings.json` ist:

```json
{
  "AppConfiguration": {
    "TtsProvider": "System",
    "TtsSettings": {
      "LocalTtsProvider": "ThorstenOnnx",
      "EnableThorstenOnnxTts": true,
      "ThorstenOnnxSettings": {
        "ModelsPath": "./TtsModels",
        "PreferredModel": "thorsten"
      }
    }
  }
}
```

### 2. Modelle herunterladen

#### Option A: Automatischer Download (empfohlen)

Die Anwendung lädt automatisch die benötigten Modelle herunter, wenn sie das erste Mal verwendet wird:

```json
{
  "ThorstenOnnxSettings": {
    "AutoDownload": true,
    "DefaultQuality": "medium",
    "DownloadTimeout": 600
  }
}
```

**Verfügbare Qualitätsstufen:**
- `"low"` - Kleine Dateien (~50 MB), schneller Download
- `"medium"` - Ausgewogene Qualität (~100 MB) **[Standard]**
- `"high"` - Beste Qualität (~200 MB), langsamerer Download

#### Option B: Manueller Download

##### B1: Piper-Modelle (einfach)

```bash
# Erstelle Modell-Verzeichnis
mkdir TtsModels
cd TtsModels

# Lade Thorsten Medium Modell herunter
curl -O https://huggingface.co/rhasspy/piper-voices/resolve/main/de/de_DE/thorsten/medium/de_DE-thorsten-medium.onnx
curl -O https://huggingface.co/rhasspy/piper-voices/resolve/main/de/de_DE/thorsten/medium/de_DE-thorsten-medium.onnx.json
```

##### B2: Coqui-TTS-Modelle (erweitert)

```bash
# Erstelle Modell-Verzeichnis
mkdir TtsModels
cd TtsModels

# Lade Thorsten Modell und Vocoder herunter
curl -O https://github.com/coqui-ai/TTS/releases/download/v0.10.1/tts_models--de--thorsten--tacotron2-DDC.tar.gz
curl -O https://github.com/coqui-ai/TTS/releases/download/v0.10.1/vocoder_models--de--thorsten--hifigan_v1.tar.gz

# Entpacke
tar -xzf tts_models--de--thorsten--tacotron2-DDC.tar.gz
tar -xzf vocoder_models--de--thorsten--hifigan_v1.tar.gz
```

### 3. Modell-Struktur

Ihr `TtsModels`-Verzeichnis sollte so aussehen:

```
TtsModels/
├── de_DE-thorsten-medium.onnx          # Hauptmodell
├── de_DE-thorsten-medium.onnx.json     # Konfiguration
├── config.json                         # Optional: Erweiterte Konfiguration
└── vocoder/                           # Optional: Für bessere Qualität
    ├── hifigan_v1.onnx
    └── config.json
```

## Verwendung

### 1. TTS-Provider wechseln

Um Thorsten ONNX zu aktivieren, ändern Sie in `appsettings.json`:

```json
"TtsProvider": "ThorstenOnnx"
```

### 2. Anwendung starten

```bash
dotnet run
```

### 3. Testen

Öffnen Sie die Chat-Seite und aktivieren Sie TTS. Der Text wird jetzt mit der Thorsten-Stimme gesprochen.

## Fehlerbehebung

### "Kein TTS-Modell gefunden"

**Problem**: Die Anwendung findet keine Thorsten-Modelle.

**Lösung**: 
1. Prüfen Sie den Pfad in `appsettings.json`
2. Stellen Sie sicher, dass `.onnx`-Dateien im `TtsModels`-Verzeichnis sind
3. Prüfen Sie die Dateinamen (sollten "thorsten" enthalten)

### Audio wird nicht abgespielt

**Problem**: Text wird verarbeitet, aber kein Sound.

**Lösung**:
1. Prüfen Sie die Lautstärke-Einstellungen
2. Auf Windows: Stellen Sie sicher, dass .NET Audio-Bibliotheken installiert sind
3. Fallback: System-TTS verwenden (`"TtsProvider": "System"`)

### Langsame Verarbeitung

**Problem**: TTS braucht lange zum Generieren.

**Lösung**:
1. Verwenden Sie kleinere Modelle (z.B. "low" statt "high")
2. Prüfen Sie verfügbaren RAM (mindestens 4 GB empfohlen)
3. SSD verwenden für bessere Ladezeiten

## Modell-Qualität

### Verfügbare Qualitätsstufen

1. **Low** (~50 MB): Schnell, grundlegende Qualität
2. **Medium** (~100 MB): Ausgewogenes Verhältnis von Geschwindigkeit und Qualität
3. **High** (~200 MB): Beste Qualität, langsamere Verarbeitung

### Empfehlungen

- **Entwicklung/Testing**: Medium-Modelle
- **Produktive Nutzung**: High-Modelle mit Vocoder
- **Eingebettete Systeme**: Low-Modelle

## Erweiterte Konfiguration

### Phonem-Mapping anpassen

Erstellen Sie `TtsModels/phonemes.json`:

```json
{
  "a": 1, "e": 2, "i": 3, "o": 4, "u": 5,
  "ä": 6, "ö": 7, "ü": 8,
  "ch": 9, "sch": 10
}
```

### Audio-Parameter optimieren

```json
"ThorstenOnnxSettings": {
  "ModelsPath": "./TtsModels",
  "PreferredModel": "thorsten",
  "SampleRate": 22050,
  "AudioFormat": "wav",
  "EnableVocoder": true
}
```

## Download-Status prüfen

Sie können den Status der Modelle über die Logs der Anwendung verfolgen:

```bash
# Starte die Anwendung und beobachte die Logs
dotnet run

# Die Logs zeigen:
# - Model-Download-Fortschritt
# - Verfügbare Modelle
# - Download-Erfolg/Fehler
```

### Programmatischer Zugriff

Für Entwickler: Der `ThorstenOnnxTtsService` bietet folgende Methoden:

```csharp
// Manueller Download
await thorstenService.DownloadModelsAsync("medium", forceDownload: false);

// Modell-Status prüfen
var modelInfo = await thorstenService.GetModelInfoAsync();
Console.WriteLine($"Modelle verfügbar: {modelInfo.ModelsAvailable}");
Console.WriteLine($"Größe: {modelInfo.TotalSizeMB:F2} MB");

// Test der Download-Funktionalität
await thorstenService.TestModelDownloadAsync();
```

## Lizenz und Hinweise

- **Thorsten-Modelle**: Open Source (verschiedene Lizenzen, meist Apache 2.0)
- **ONNX Runtime**: MIT License
- **Nutzung**: Sowohl private als auch kommerzielle Nutzung meist erlaubt

Prüfen Sie immer die spezifischen Lizenz-Bedingungen der verwendeten Modelle.

## Support

Bei Problemen:
1. Prüfen Sie die Logs in der Konsole
2. Testen Sie mit System-TTS als Fallback
3. Überprüfen Sie die Modell-Dateien und -Pfade

---

**Tipp**: Starten Sie mit den empfohlenen Piper-Modellen für die einfachste Einrichtung!
