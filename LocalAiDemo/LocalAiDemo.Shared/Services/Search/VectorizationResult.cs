namespace LocalAiDemo.Shared.Services.Search;

/// <summary>
/// Result of a vectorization operation
/// </summary>
public class VectorizationResult
{
    public bool Success { get; set; }
    public int ChatsProcessed { get; set; }
    public int MessagesProcessed { get; set; }
    public VectorizationStats? InitialStats { get; set; }
    public VectorizationStats? FinalStats { get; set; }
    public TimeSpan Duration { get; set; }
    public string? ErrorMessage { get; set; }

    public int TotalItemsProcessed => ChatsProcessed + MessagesProcessed;

    public string GetSummary()
    {
        if (!Success)
        {
            return $"Vectorization failed: {ErrorMessage}";
        }

        return $"Successfully processed {ChatsProcessed} chats and {MessagesProcessed} messages in {Duration:mm\\:ss}";
    }
}