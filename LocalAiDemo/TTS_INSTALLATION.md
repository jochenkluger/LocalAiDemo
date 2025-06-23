# Lokale TTS-Services Installation

Diese Anleitung beschreibt, wie Sie die verschiedenen lokalen Text-to-Speech Engines für LocalAiDemo installieren und konfigurieren.

## 🎯 Verfügbare lokale TTS-Provider

1. **System TTS** ✅ (bereits aktiv)
2. **eSpeak TTS** (leichtgewichtig, Open Source)
3. **Piper TTS** (hochqualitativ, Home Assistant)
4. **ONNX TTS** (Microsoft Neural TTS)

## 📦 Installation der TTS-Engines

### 1. eSpeak Installation

#### Windows:
```bash
# Download von: https://github.com/espeak-ng/espeak-ng/releases
# Installiere espeak-ng-X.XX.msi
# Oder via Chocolatey:
choco install espeak
```

#### Linux (Ubuntu/Debian):
```bash
sudo apt-get update
sudo apt-get install espeak-ng
```

#### macOS:
```bash
# Via Homebrew:
brew install espeak-ng
```

### 2. Piper TTS Installation

#### Alle Plattformen:

1. **Piper Binary herunterladen:**
   - Windows: https://github.com/rhasspy/piper/releases (piper_windows_amd64.zip)
   - Linux: https://github.com/rhasspy/piper/releases (piper_linux_x86_64.tar.gz)
   - macOS: https://github.com/rhasspy/piper/releases (piper_macos_x64.tar.gz)

2. **Piper extrahieren:**
   ```bash
   # Windows
   mkdir C:\Tools\piper
   # Extrahiere ZIP nach C:\Tools\piper\
   
   # Linux/macOS
   sudo mkdir -p /opt/piper
   sudo tar -xzf piper_*.tar.gz -C /opt/piper
   sudo ln -s /opt/piper/piper /usr/local/bin/piper
   ```

3. **Deutsche Stimm-Modelle herunterladen:**
   
   Erstelle Ordner: `./PiperModels/` im App-Verzeichnis
   
   **Empfohlene deutsche Modelle:**
   - **Thorsten (männlich):** 
     ```bash
     # Klein (schnell)
     wget https://huggingface.co/rhasspy/piper-voices/resolve/main/de/de_DE/thorsten/low/de_DE-thorsten-low.onnx
     wget https://huggingface.co/rhasspy/piper-voices/resolve/main/de/de_DE/thorsten/low/de_DE-thorsten-low.onnx.json
     
     # Mittlere Qualität (empfohlen)
     wget https://huggingface.co/rhasspy/piper-voices/resolve/main/de/de_DE/thorsten/medium/de_DE-thorsten-medium.onnx
     wget https://huggingface.co/rhasspy/piper-voices/resolve/main/de/de_DE/thorsten/medium/de_DE-thorsten-medium.onnx.json
     ```
   
   - **Kerstin (weiblich):**
     ```bash
     wget https://huggingface.co/rhasspy/piper-voices/resolve/main/de/de_DE/kerstin/low/de_DE-kerstin-low.onnx
     wget https://huggingface.co/rhasspy/piper-voices/resolve/main/de/de_DE/kerstin/low/de_DE-kerstin-low.onnx.json
     ```

### 3. ONNX TTS (Optional)

Für ONNX-basierte TTS benötigen Sie:

1. **ONNX Runtime:** Bereits in .NET enthalten
2. **TTS-Modelle:** 
   - Microsoft FastSpeech2: https://github.com/microsoft/onnxruntime/tree/main/onnxruntime/python/tools/transformers/models/t5
   - Facebook MMS TTS: https://huggingface.co/facebook/mms-tts

## ⚙️ Konfiguration

### appsettings.json bearbeiten:

```json
{
  "AppConfiguration": {
    "TtsProvider": "Piper",  // "System", "eSpeak", "Piper", "ONNX", "Browser"
    "TtsSettings": {
      "ESpeakSettings": {
        "Speed": 150,
        "Pitch": 50,
        "Amplitude": 100,
        "Voice": "de"
      },
      "PiperSettings": {
        "PreferredModel": "de_DE-thorsten-medium.onnx",
        "ModelsPath": "./PiperModels"
      }
    }
  }
}
```

## 🎭 Stimm-Qualität Vergleich

| Provider | Qualität | Geschwindigkeit | Größe | Offline |
|----------|----------|----------------|-------|---------|
| System TTS | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | 0 MB | ✅ |
| eSpeak | ⭐⭐ | ⭐⭐⭐⭐⭐ | < 5 MB | ✅ |
| Piper | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | 20-60 MB | ✅ |
| Browser TTS | ⭐⭐⭐ | ⭐⭐⭐⭐ | 0 MB | ✅ |

## 🚀 Erste Schritte

1. **Standard:** System TTS ist bereits aktiviert und funktioniert sofort
2. **Bessere Qualität:** Installiere Piper + deutsche Modelle für beste Sprachqualität
3. **Minimaler Aufwand:** Installiere eSpeak für schnelle lokale Alternative

## 🔧 Problembehandlung

### eSpeak funktioniert nicht:
```bash
# Teste eSpeak Installation:
espeak-ng --version
espeak-ng -v de "Hallo Welt"
```

### Piper funktioniert nicht:
```bash
# Teste Piper Installation:
piper --help

# Teste mit Modell:
echo "Hallo Welt" | piper --model ./PiperModels/de_DE-thorsten-low.onnx --output_file test.wav
```

### Logs überprüfen:
Die App protokolliert TTS-Aktivitäten. Schaue in die Logs für Details:
- "TTS service is available: True/False"
- "Using [Provider] TTS provider"

## 🎯 Empfehlung

Für die beste Balance zwischen Qualität und Einfachheit:

1. **Sofort:** Nutze System TTS (bereits aktiv)
2. **Upgrade:** Installiere Piper mit `de_DE-thorsten-medium.onnx`
3. **Backup:** Installiere eSpeak als Fallback

Dann setze in appsettings.json: `"TtsProvider": "Piper"`
