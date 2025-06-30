namespace LocalAiDemo.Shared.Services.Search;

public interface IEmbeddingService
{
    float[] GenerateEmbedding(string text);

    Task<float[]> GetEmbeddingAsync(string text);

    float CalculateCosineSimilarity(float[] vector1, float[]? vector2);
}