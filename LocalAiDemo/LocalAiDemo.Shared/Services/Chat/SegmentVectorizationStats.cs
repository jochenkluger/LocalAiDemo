namespace LocalAiDemo.Shared.Services.Chat;

/// <summary>
/// Statistics about segment vectorization
/// </summary>
public class SegmentVectorizationStats
{
    public int TotalChats { get; set; }
    public int TotalSegments { get; set; }
    public int SegmentsWithContent { get; set; }
    public int SegmentsWithVectors { get; set; }
    public int SegmentsWithoutVectors { get; set; }
    public double VectorizationPercentage { get; set; }
    public double AverageSegmentsPerChat { get; set; }
    public double AverageMessagesPerSegment { get; set; }
}