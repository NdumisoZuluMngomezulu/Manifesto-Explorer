namespace ManifestoExplorer.Api.Models;

public class Policy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ManifestoId { get; set; }
    public Manifesto? Manifesto { get; set; }
    public required string Title { get; set; }
    public required string Topic { get; set; }
    public required string Summary { get; set; }
    public required string FullText { get; set; }
    public string? PageReference { get; set; }
    public string? EvidenceQuote { get; set; }
    public string ExtractionMethod { get; set; } = "Keyword";
    public decimal? Confidence { get; set; }
    public string VerificationStatus { get; set; } = "NeedsReview";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? VerifiedAt { get; set; }
}
