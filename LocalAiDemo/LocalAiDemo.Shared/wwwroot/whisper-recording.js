// Whisper audio recording functionality for LocalAiDemo
console.log('whisper-recording.js loaded');

let whisperAudioContext = null;
let whisperMediaRecorder = null;
let whisperAudioChunks = [];
let whisperDotNetReference = null;

// Initialize Whisper recording capabilities
window.initializeWhisperRecording = (dotNetObj) => {
    try {
        whisperDotNetReference = dotNetObj;

        // Check if browser supports media recording
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            console.error('Media recording not supported in this browser');
            return false;
        }

        if (!window.AudioContext && !window.webkitAudioContext) {
            console.error('Web Audio API not supported in this browser');
            return false;
        }

        console.log('Whisper recording successfully initialized');
        return true;
    } catch (error) {
        console.error('Error initializing Whisper recording:', error);
        return false;
    }
};

// Check if Whisper recording is supported by the browser
window.isWhisperRecordingSupported = () => {
    try {
        // Check if browser supports media recording
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            return false;
        }

        // Check if Web Audio API is supported
        if (!window.AudioContext && !window.webkitAudioContext) {
            return false;
        }

        // Check if MediaRecorder is supported
        if (!window.MediaRecorder) {
            return false;
        }

        return true;
    } catch (error) {
        console.error('Error checking Whisper recording support:', error);
        return false;
    }
};

// Start audio capture for Whisper processing
window.startWhisperRecording = async () => {
    console.log('startWhisperRecording called');
    try {
        // Initialize audio context
        whisperAudioContext = new (window.AudioContext || window.webkitAudioContext)();
        console.log('Audio context created:', whisperAudioContext);

        // Request microphone access
        const stream = await navigator.mediaDevices.getUserMedia({
            audio: {
                echoCancellation: true,
                noiseSuppression: true,
                autoGainControl: true
            }
        });

        // Create media recorder
        whisperMediaRecorder = new MediaRecorder(stream, {
            mimeType: 'audio/webm;codecs=opus'
        });

        // Reset audio chunks
        whisperAudioChunks = [];

        // Set up data collection
        whisperMediaRecorder.ondataavailable = (event) => {
            if (event.data.size > 0) {
                whisperAudioChunks.push(event.data);
            }
        };

        // Start recording
        whisperMediaRecorder.start();
        console.log('Whisper recording started');

        // Add visual feedback
        const recordButton = document.getElementById('speechButton');
        if (recordButton) {
            recordButton.classList.add('recording');
        }

        return true;
    } catch (error) {
        console.error('Error starting Whisper recording:', error);
        return false;
    }
};

// Stop audio capture and process for Whisper
window.stopWhisperRecording = async () => {
    try {
        if (!whisperMediaRecorder || whisperMediaRecorder.state === 'inactive') {
            console.error('No active recording to stop');
            return null;
        }

        return new Promise((resolve) => {
            whisperMediaRecorder.onstop = async () => {
                try {
                    // Create blob from recorded chunks
                    const blob = new Blob(whisperAudioChunks, { type: 'audio/webm;codecs=opus' });

                    // Convert to array buffer
                    const arrayBuffer = await blob.arrayBuffer();

                    // Decode audio to PCM
                    const audioBuffer = await whisperAudioContext.decodeAudioData(arrayBuffer);

                    // Resample to 16 kHz for Whisper
                    const resampledBuffer = await resampleAudioBuffer(audioBuffer, 16000);

                    // Convert resampled data to WAV format
                    const wavBuffer = createWavHeader(resampledBuffer, 16000, 1);
                    const audioData = new Uint8Array(wavBuffer);

                    console.log('Whisper recording processed successfully');

                    // Remove visual feedback
                    const recordButton = document.getElementById('speechButton');
                    if (recordButton) {
                        recordButton.classList.remove('recording');
                    }

                    // Send processed audio back to C# code
                    if (whisperDotNetReference) {
                        try {
                            // First, process the audio data with Whisper
                            const transcription = await whisperDotNetReference.invokeMethodAsync('ProcessAudioData', audioData);
                            console.log('Whisper transcription result:', transcription);

                            // Then call OnSpeechRecognized with the transcription result
                            if (transcription && transcription.trim() !== '') {
                                await whisperDotNetReference.invokeMethodAsync('OnSpeechRecognized', transcription);
                            }
                        } catch (error) {
                            console.error('Error calling Whisper processing methods:', error);
                        }
                    }

                    resolve(audioData);
                } catch (error) {
                    console.error('Error processing audio:', error);
                    resolve(null);
                }
            };

            whisperMediaRecorder.stop();
            console.log('Whisper recording stopped');
        });
    } catch (error) {
        console.error('Error stopping Whisper recording:', error);
        return null;
    }
};

// Resample audio buffer to target sample rate
async function resampleAudioBuffer(audioBuffer, targetSampleRate) {
    try {
        const offlineContext = new OfflineAudioContext(
            1, // Mono channel for Whisper
            Math.ceil(audioBuffer.duration * targetSampleRate),
            targetSampleRate
        );

        const source = offlineContext.createBufferSource();
        source.buffer = audioBuffer;

        source.connect(offlineContext.destination);
        source.start(0);

        const renderedBuffer = await offlineContext.startRendering();
        return renderedBuffer.getChannelData(0); // Return PCM data for the first channel
    } catch (error) {
        console.error('Error resampling audio:', error);
        throw error;
    }
}

// Create WAV header for PCM data
function createWavHeader(pcmData, sampleRate, channels) {
    const dataLength = pcmData.length * 2; // 16-bit PCM, so 2 bytes per sample
    const buffer = new ArrayBuffer(44 + dataLength);
    const view = new DataView(buffer);

    // RIFF header
    writeString(view, 0, "RIFF");
    view.setUint32(4, 36 + dataLength, true);
    writeString(view, 8, "WAVE");

    // fmt chunk
    writeString(view, 12, "fmt ");
    view.setUint32(16, 16, true); // Subchunk1Size (16 for PCM)
    view.setUint16(20, 1, true); // Audio format (1 = PCM)
    view.setUint16(22, channels, true); // Number of channels
    view.setUint32(24, sampleRate, true); // Sample rate
    view.setUint32(28, sampleRate * channels * 2, true); // Byte rate
    view.setUint16(32, channels * 2, true); // Block align
    view.setUint16(34, 16, true); // Bits per sample

    // data chunk
    writeString(view, 36, "data");
    view.setUint32(40, dataLength, true);

    // Write PCM samples
    const output = new DataView(buffer, 44);
    let offset = 0;
    for (let i = 0; i < pcmData.length; i++) {
        const sample = Math.max(-1, Math.min(1, pcmData[i])); // Clamp values to [-1, 1]
        output.setInt16(offset, sample * 0x7fff, true); // Convert to 16-bit PCM
        offset += 2;
    }

    return buffer;
}

// Helper function to write string to DataView
function writeString(view, offset, string) {
    for (let i = 0; i < string.length; i++) {
        view.setUint8(offset + i, string.charCodeAt(i));
    }
}

// Clean up resources
window.cleanupWhisperRecording = () => {
    try {
        if (whisperMediaRecorder && whisperMediaRecorder.state !== 'inactive') {
            whisperMediaRecorder.stop();
        }

        if (whisperAudioContext && whisperAudioContext.state !== 'closed') {
            whisperAudioContext.close();
        }

        whisperAudioChunks = [];
        whisperDotNetReference = null;

        console.log('Whisper recording resources cleaned up');
        return true;
    } catch (error) {
        console.error('Error cleaning up Whisper recording:', error);
        return false;
    }
};