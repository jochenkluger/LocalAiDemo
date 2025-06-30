using LocalAiDemo.Shared.Models;

namespace LocalAiDemo.Shared.Services.Chat;

/// <summary>
/// Result of a segment similarity search
/// </summary>
public class SegmentSimilarityResult
{
    public ChatSegment Segment { get; set; } = new();
    public float Similarity { get; set; }
    public string MatchType { get; set; } = "Vector"; // Vector, Text, Hybrid
    public string? MatchedKeywords { get; set; }
}