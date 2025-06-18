// Speech recognition functionality for LocalAiDemo
let recognition;
let dotNetReference;

window.initSpeechRecognition = (dotNetObj) => {
    try {
        dotNetReference = dotNetObj;
        
        // Check if browser supports speech recognition
        if (!('webkitSpeechRecognition' in window) && !('SpeechRecognition' in window)) {
            console.error('Speech recognition not supported in this browser');
            return false;
        }
        
        // Create speech recognition instance
        recognition = new (window.SpeechRecognition || window.webkitSpeechRecognition)();
        
        // Configure recognition
        recognition.continuous = false;
        recognition.interimResults = false;
        recognition.lang = 'de-DE'; // German language
        
        // Set up event handlers
        recognition.onresult = (event) => {
            if (event.results.length > 0) {
                const result = event.results[event.results.length - 1];
                if (result.isFinal) {
                    const transcript = result[0].transcript;
                    console.log('Recognized speech:', transcript);
                    
                    // Send recognized text back to C# code
                    dotNetReference.invokeMethodAsync('OnSpeechRecognized', transcript);
                }
            }
        };
        
        recognition.onerror = (event) => {
            console.error('Speech recognition error:', event.error);
        };
        
        recognition.onend = () => {
            console.log('Speech recognition ended');
        };
        
        return true;
    } catch (error) {
        console.error('Error initializing speech recognition:', error);
        return false;
    }
};

window.startSpeechRecognition = () => {
    try {
        if (recognition) {
            recognition.start();
            console.log('Speech recognition started');
            
            // Add 'recording' class to the button for visual feedback
            const speechButton = document.getElementById('speechButton');
            if (speechButton) {
                speechButton.classList.add('recording');
            }
            
            return true;
        }
        return false;
    } catch (error) {
        console.error('Error starting speech recognition:', error);
        return false;
    }
};

window.stopSpeechRecognition = () => {
    try {
        if (recognition) {
            recognition.stop();
            console.log('Speech recognition stopped');
            
            // Remove 'recording' class from the button
            const speechButton = document.getElementById('speechButton');
            if (speechButton) {
                speechButton.classList.remove('recording');
            }
            
            return true;
        }
        return false;
    } catch (error) {
        console.error('Error stopping speech recognition:', error);
        return false;
    }
};
