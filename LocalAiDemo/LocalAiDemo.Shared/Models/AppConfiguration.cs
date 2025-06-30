namespace LocalAiDemo.Shared.Models
{
    public class AppConfiguration
    {
        // Grundlegende Einstellungen
        public string TtsProvider { get; set; } = "Browser";

        public string SttProvider { get; set; } = "Whisper";
        public string GenerationProvider { get; set; } = "phi4";

        // AI-spezifische Einstellungen
        public bool UseSemanticKernel { get; set; } = false;

        // TTS-Einstellungen
        public TtsSettings TtsSettings { get; set; } = new();
    }

    public class TtsSettings
    {
        public string LocalTtsProvider { get; set; } = "ThorstenOnnx";
        public bool EnableESpeakTts { get; set; } = true;
        public bool EnablePiperTts { get; set; } = true;
        public bool EnableOnnxTts { get; set; } = true;
        public bool EnableThorstenOnnxTts { get; set; } = true;

        public ESpeakSettings ESpeakSettings { get; set; } = new();
        public PiperSettings PiperSettings { get; set; } = new();
        public OnnxSettings OnnxSettings { get; set; } = new();
        public ThorstenOnnxSettings ThorstenOnnxSettings { get; set; } = new();
    }

    public class ESpeakSettings
    {
        public int Speed { get; set; } = 150;
        public int Pitch { get; set; } = 50;
        public int Amplitude { get; set; } = 100;
        public string Voice { get; set; } = "de";
    }

    public class PiperSettings
    {
        public string PreferredModel { get; set; } = "de_DE-thorsten-medium.onnx";
        public string ModelsPath { get; set; } = "./PiperModels";
    }

    public class OnnxSettings
    {
        public string ModelsPath { get; set; } = "./TtsModels";
        public string PreferredModel { get; set; } = "de_DE_neural.onnx";
    }

    public class ThorstenOnnxSettings
    {
        public string ModelsPath { get; set; } = "./TtsModels";
        public string PreferredModel { get; set; } = "thorsten";
        public bool AutoDownload { get; set; } = true;
        public string DefaultQuality { get; set; } = "medium";
        public int DownloadTimeout { get; set; } = 600;
    }
}