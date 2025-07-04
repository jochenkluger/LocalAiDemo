﻿namespace LocalAiDemo.Shared.Models
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
                ExecutionMode.Local
            ),

            new AiModel(
                "Phi-4-mini-reasoning (Q4_K_XL)",
                "Phi-4-mini-reasoning-UD-Q4_K_XL.gguf",
                "https://huggingface.co/unsloth/Phi-4-mini-reasoning-GGUF/resolve/main/Phi-4-mini-reasoning-UD-Q4_K_XL.gguf?download=true",
                ExecutionMode.Local
            ),

            new AiModel(
                "Llama 3.2 3B (Q4_K) Pilemouse",
                "llama-3.2-3b-q4_k_m.gguf",
                "https://huggingface.co/pilemouse/Llama-3.2-3B-Q4_K_M-GGUF/blob/main/llama-3.2-3b-q4_k_m.gguf?download=true",
                ExecutionMode.Local
            ),

            new AiModel(
                "Llama 3.2 3B instruct (Q4_1) Mungert",
                "Llama-3.2-3B-Instruct-q4_1.gguf",
                "https://huggingface.co/Mungert/Llama-3.2-3B-Instruct-GGUF/blob/main/Llama-3.2-3B-Instruct-q4_1.gguf?download=true",
                ExecutionMode.Local
            ),

            new AiModel(
                "Llama-3.2-3B-Instruct-GGUF (Q5_K_M) Bartowski",
                "Llama-3.2-3B-Instruct-Q5_K_M.gguf",
                "https://huggingface.co/bartowski/Llama-3.2-3B-Instruct-GGUF/resolve/main/Llama-3.2-3B-Instruct-Q5_K_M.gguf?download=true",
                ExecutionMode.Local
            ),

            new AiModel(
                "Gemma 3n E2B (Q3_K_M)",
                "gemma-3n-E2B-it-Q3_K_M.gguf",
                "https://huggingface.co/unsloth/gemma-3n-E2B-it-GGUF/resolve/main/gemma-3n-E2B-it-Q3_K_M.gguf?download=true",
                ExecutionMode.Local
            ),

            new AiModel(
                "Tinyllama german (Q6_K)",
                "tinyllama-german.Q6_K.gguf",
                "https://huggingface.co/mradermacher/tinyllama-german-GGUF/resolve/main/tinyllama-german.Q6_K.gguf?download=true",
                ExecutionMode.Local
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