namespace LocalAiDemo.Shared.Services.Search
{
    public interface IEmbeddingService
    {
        float[] GenerateEmbedding(string text);

        float CalculateCosineSimilarity(float[] vector1, float[]? vector2);
    }

    public class EmbeddingService : IEmbeddingService
    {
        private readonly Random _random = new Random();
        private const int EmbeddingDimension = 128; // Using 128-dimensional vectors

        public float[] GenerateEmbedding(string text)
        {
            // In a real application, this would call an AI model to generate text embeddings
            // Here we're generating deterministic "fake" embeddings based on the hash of the text

            // Create a deterministic seed from the string hash to maintain consistency
            int seed = text.GetHashCode();
            var seedRandom = new Random(seed);

            // Generate a 128-dimensional embedding vector
            var embedding = new float[EmbeddingDimension];
            for (int i = 0; i < EmbeddingDimension; i++)
            {
                embedding[i] = (float)(seedRandom.NextDouble() * 2 - 1); // Values between -1 and 1
            }

            // Normalize the vector
            float magnitude = 0;
            foreach (var value in embedding)
            {
                magnitude += value * value;
            }
            magnitude = (float)Math.Sqrt(magnitude);

            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] /= magnitude;
            }

            return embedding;
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
    }
}