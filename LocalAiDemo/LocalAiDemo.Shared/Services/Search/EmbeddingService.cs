using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Text.RegularExpressions;

namespace LocalAiDemo.Shared.Services.Search
{
    public class EmbeddingService : IEmbeddingService, IDisposable
    {
        private readonly InferenceSession? _session;
        private readonly ILogger<EmbeddingService> _logger;
        private const int EmbeddingDimension = 384; // all-MiniLM-L6-v2 produces 384-dimensional embeddings
        private const int MaxSequenceLength = 512;
        private bool _disposed = false;

        public EmbeddingService(ILogger<EmbeddingService> logger)
        {
            _logger = logger;

            try
            {
                // Path to the ONNX model - you'll need to download this
                var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "all-MiniLM", "all-MiniLM-L6-v2.onnx");
                if (File.Exists(modelPath))
                {
                    _session = new InferenceSession(modelPath);
                    _logger.LogInformation("Loaded all-MiniLM-L6-v2 ONNX model from {ModelPath}", modelPath);
                }
                else
                {
                    var modelsDir = Path.GetDirectoryName(modelPath);
                    if (!Directory.Exists(modelsDir))
                        Directory.CreateDirectory(modelsDir!);

                    _logger.LogError("ONNX model not found at {ModelPath}. Please download the all-MiniLM-L6-v2 ONNX model from Hugging Face.", modelPath);
                    throw new FileNotFoundException($"ONNX model not found at {modelPath}. Please download the all-MiniLM-L6-v2 ONNX model from Hugging Face.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize ONNX model");
                throw;
            }
        }

        public float[] GenerateEmbedding(string text)
        {
            return GetEmbeddingAsync(text).GetAwaiter().GetResult();
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new float[EmbeddingDimension];

            if (_session == null)
            {
                throw new InvalidOperationException("ONNX model is not loaded. Please ensure the all-MiniLM-L6-v2 model files are available.");
            }

            return await Task.Run(() => GenerateOnnxEmbedding(text));
        }

        private float[] GenerateOnnxEmbedding(string text)
        {
            try
            {
                _logger.LogDebug("Generating ONNX embedding for text: {TextPreview}...",
                    text.Length > 50 ? text.Substring(0, 50) + "..." : text);

                // Use basic tokenization
                var tokens = BasicTokenize(text);

                // Add special tokens: [CLS] + tokens + [SEP]
                var inputTokens = new List<int> { 101 }; // [CLS]
                inputTokens.AddRange(tokens);
                inputTokens.Add(102); // [SEP]

                // Pad to max length
                while (inputTokens.Count < MaxSequenceLength)
                    inputTokens.Add(0); // [PAD]

                // Truncate if too long
                if (inputTokens.Count > MaxSequenceLength)
                    inputTokens = inputTokens.Take(MaxSequenceLength).ToList();                // Create attention mask
                var attentionMask = inputTokens.Select(t => t != 0 ? 1L : 0L).ToArray();
                var inputIds = inputTokens.Select(t => (long)t).ToArray();

                // Create token type ids (all zeros for single sentence)
                var tokenTypeIds = new long[inputIds.Length]; // All zeros

                // Create tensors
                var inputTensor = new DenseTensor<long>(inputIds, new[] { 1, inputIds.Length });
                var attentionTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });
                var tokenTypeTensor = new DenseTensor<long>(tokenTypeIds, new[] { 1, tokenTypeIds.Length });

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input_ids", inputTensor),
                    NamedOnnxValue.CreateFromTensor("attention_mask", attentionTensor),
                    NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeTensor)
                };

                // Run inference
                using var results = _session!.Run(inputs);
                var outputTensor = results.First().AsTensor<float>();

                // Apply mean pooling
                var pooledEmbedding = MeanPooling(outputTensor, attentionMask);

                // Normalize
                var normalizedEmbedding = NormalizeVector(pooledEmbedding);

                _logger.LogDebug("Successfully generated {Dimension}-dimensional ONNX embedding", normalizedEmbedding.Length);
                return normalizedEmbedding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating ONNX embedding: {ErrorMessage}", ex.Message);
                throw new InvalidOperationException($"Failed to generate embedding: {ex.Message}", ex);
            }
        }

        private int[] BasicTokenize(string text)
        {
            // Very basic tokenization - split by whitespace and convert to hash-based IDs
            var words = Regex.Replace(text.ToLowerInvariant(), @"[^\w\s]", " ")
                .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            return words.Take(MaxSequenceLength - 2)
                .Select(word => Math.Abs(word.GetHashCode()) % 30000 + 1000) // Map to vocab range
                .ToArray();
        }

        private float[] MeanPooling(Tensor<float> lastHiddenState, long[] attentionMask)
        {
            var seqLength = lastHiddenState.Dimensions[1];
            var hiddenSize = lastHiddenState.Dimensions[2];

            var pooled = new float[hiddenSize];
            var maskSum = attentionMask.Sum(m => m);

            if (maskSum == 0) maskSum = 1; // Avoid division by zero

            for (int h = 0; h < hiddenSize; h++)
            {
                float sum = 0;
                for (int s = 0; s < seqLength; s++)
                {
                    if (attentionMask[s] == 1)
                    {
                        sum += lastHiddenState[0, s, h];
                    }
                }
                pooled[h] = sum / maskSum;
            }

            return pooled;
        }
        private float[] NormalizeVector(float[] vector)
        {
            var magnitude = (float)Math.Sqrt(vector.Sum(x => x * x));
            if (magnitude > 0)
            {
                for (int i = 0; i < vector.Length; i++)
                {
                    vector[i] /= magnitude;
                }
            }
            return vector;
        }

        public float CalculateCosineSimilarity(float[] vector1, float[]? vector2)
        {
            if (vector1 == null || vector2 == null || vector1.Length != vector2.Length)
                return 0;

            float dotProduct = 0;
            float magnitude1 = 0;
            float magnitude2 = 0;

            for (int i = 0; i < vector1.Length; i++)
            {
                dotProduct += vector1[i] * vector2[i];
                magnitude1 += vector1[i] * vector1[i];
                magnitude2 += vector2[i] * vector2[i];
            }

            magnitude1 = (float)Math.Sqrt(magnitude1);
            magnitude2 = (float)Math.Sqrt(magnitude2);

            if (magnitude1 == 0 || magnitude2 == 0)
                return 0;

            return dotProduct / (magnitude1 * magnitude2);
        }
        public void Dispose()
        {
            if (!_disposed)
            {
                _session?.Dispose();
                _disposed = true;
            }
        }
    }
}