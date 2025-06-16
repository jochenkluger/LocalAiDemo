using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OllamaSharp.Models;

namespace LocalAiDemo.Shared.Models
{
    public static class AvailableModels
    {
        public static List<AiModel> Models { get; set; } =
        [
            new AiModel(
                "Phi-3.5 Mini Instruct (Q5_K_L)",
                "Phi-3.5-mini-instruct-Q5_K_L.gguf",
                "https://huggingface.co/bartowski/Phi-3.5-mini-instruct-GGUF/resolve/main/Phi-3.5-mini-instruct-Q5_K_L.gguf?download=true",
                ExecutionMode.Local
            ),

            new AiModel(
                "Phi-3 Mini 4K Instruct (Q4)",
                "Phi-3-mini-4k-instruct-q4.gguf",
                "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-gguf/resolve/main/Phi-3-mini-4k-instruct-q4.gguf?download=true",
                ExecutionMode.Local
            ),

            new AiModel(
                "Phi-3.5 Mini Instruct (Q2_K)",
                "Phi-3.5-mini-instruct-Q2_K.gguf",
                "https://huggingface.co/bartowski/Phi-3.5-mini-instruct-GGUF/resolve/main/Phi-3.5-mini-instruct-Q2_K.gguf?download=true",
                ExecutionMode.Local
            ),

            new AiModel(
                "Gemma 3 12B Instruct (Q2_K_L)",
                "gemma-3-12b-it-Q2_K_L.gguf",
                "https://huggingface.co/unsloth/gemma-3-12b-it-GGUF/resolve/main/gemma-3-12b-it-Q2_K_L.gguf?download=true",
                ExecutionMode.Local,
                true // Default selected Logic model
            ),

            new AiModel(
                "Phi-4 (Q4_K)",
                "phi-4-Q4_K.gguf",
                "https://huggingface.co/microsoft/phi-4-gguf/resolve/main/phi-4-Q4_K.gguf?download=true",
                ExecutionMode.Local,
                true // Default selected Logic model
            ),

            new AiModel(
                "Phi-4-mini-reasoning (Q4_K_XL)",
                "Phi-4-mini-reasoning-UD-Q4_K_XL.gguf",
                "https://huggingface.co/unsloth/Phi-4-mini-reasoning-GGUF/resolve/main/Phi-4-mini-reasoning-UD-Q4_K_XL.gguf?download=true",
                ExecutionMode.Local,
                true // Default selected Logic model
            ),

            new AiModel(
                "Tinyllama german (Q6_K)",
                "tinyllama-german.Q6_K.gguf",
                "https://huggingface.co/mradermacher/tinyllama-german-GGUF/resolve/main/tinyllama-german.Q6_K.gguf?download=true",
                ExecutionMode.Local,
                true // Default selected Logic model
            ),

            new AiModel(
                "OpenAI GPT-4",
                "",
                "",
                ExecutionMode.Server
            )
        ];
    }
}