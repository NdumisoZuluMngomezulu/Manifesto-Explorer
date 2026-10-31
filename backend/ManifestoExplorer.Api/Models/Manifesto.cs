namespace ManifestoExplorer.Api.Models;

public class Manifesto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PartyId { get; set; }
    public Party? Party { get; set; }
    public required string Title { get; set; }
    public int ElectionYear { get; set; }
    public string? DocumentUrl { get; set; }
    public string? SourceName { get; set; }
    public string? StoredFileName { get; set; }
    public string? ContentSha256 { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<Policy> Policies { get; set; } = [];
}
