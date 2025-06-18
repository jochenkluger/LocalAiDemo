using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}