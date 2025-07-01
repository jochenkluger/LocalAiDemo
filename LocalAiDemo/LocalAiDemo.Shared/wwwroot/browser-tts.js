// Browser Text-to-Speech Funktionalität
let speechSynthesis;
let voices = [];
let currentUtterance = null;

// Initialisiert die Browser TTS und prüft, ob sie verfügbar ist
window.initBrowserTts = () => {
    try {
        // Prüfen, ob Speech Synthesis API verfügbar ist
        if ('speechSynthesis' in window) {
            speechSynthesis = window.speechSynthesis;

            // Abrufen der verfügbaren Stimmen
            voices = speechSynthesis.getVoices();

            // In Chrome werden die Stimmen asynchron geladen
            if (voices.length === 0) {
                speechSynthesis.addEventListener('voiceschanged', () => {
                    voices = speechSynthesis.getVoices();
                    console.log(`TTS: Geladen ${voices.length} Stimmen`);
                });
            } else {
                console.log(`TTS: Geladen ${voices.length} Stimmen`);
            }

            console.log('Browser TTS erfolgreich initialisiert');
            return true;
        } else {
            console.error('Keine Browser TTS-Unterstützung verfügbar');
            return false;
        }
    } catch (error) {
        console.error('Fehler bei der Initialisierung von Browser TTS:', error);
        return false;
    }
};

// Spricht den angegebenen Text mit der ausgewählten Stimme
window.speakText = (text) => {
    try {
        if (!speechSynthesis) {
            console.error('TTS ist nicht initialisiert');
            return false;
        }

        // Stoppe aktuelle Sprache, falls vorhanden
        if (currentUtterance) {
            window.stopSpeaking();
        }

        // Erstelle neue Äußerung
        const utterance = new SpeechSynthesisUtterance(text);
        currentUtterance = utterance;

        // Finde deutsche Stimme, falls verfügbar
        const germanVoice = voices.find(voice =>
            voice.lang.startsWith('de') && voice.localService);

        // Alternativ verwende die erste deutsche Stimme
        const anyGermanVoice = voices.find(voice =>
            voice.lang.startsWith('de'));

        // Setze Stimme, wenn verfügbar
        if (germanVoice) {
            utterance.voice = germanVoice;
            console.log(`TTS: Verwende lokale deutsche Stimme: ${germanVoice.name}`);
        } else if (anyGermanVoice) {
            utterance.voice = anyGermanVoice;
            console.log(`TTS: Verwende deutsche Stimme: ${anyGermanVoice.name}`);
        } else {
            console.log('TTS: Keine deutsche Stimme gefunden, verwende Standard-Stimme');
        }

        // Setze die Sprache auf Deutsch
        utterance.lang = 'de-DE';

        // Setze Ereignisbehandlung
        utterance.onend = () => {
            console.log('TTS: Sprechen beendet');
            currentUtterance = null;
        };

        utterance.onerror = (event) => {
            console.error('TTS Fehler:', event.error);
            currentUtterance = null;
        };

        // Starte das Sprechen
        speechSynthesis.speak(utterance);
        console.log('TTS: Spreche Text: ' + text);

        return true;
    } catch (error) {
        console.error('Fehler beim Sprechen:', error);
        return false;
    }
};

// Stoppt das aktuelle Sprechen
window.stopSpeaking = () => {
    try {
        if (speechSynthesis) {
            // Verwende sowohl cancel() als auch pause() für sicheres Stoppen
            speechSynthesis.cancel();
            speechSynthesis.pause();
            
            // Setze currentUtterance auf null
            currentUtterance = null;
            
            // Force clear the speech queue (workaround für manche Browser)
            setTimeout(() => {
                speechSynthesis.cancel();
            }, 10);
            
            console.log('TTS: Sprechen gestoppt');
            return true;
        }
        return false;
    } catch (error) {
        console.error('Fehler beim Stoppen des Sprechens:', error);
        return false;
    }
};

// Gibt alle verfügbaren Stimmen zurück (für Diagnostik)
window.getAvailableVoices = () => {
    if (!speechSynthesis) {
        return [];
    }

    const voiceList = speechSynthesis.getVoices();
    return voiceList.map(voice => ({
        name: voice.name,
        lang: voice.lang,
        localService: voice.localService,
        default: voice.default
    }));
};