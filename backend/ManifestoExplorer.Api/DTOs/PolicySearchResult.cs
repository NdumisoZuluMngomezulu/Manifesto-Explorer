namespace ManifestoExplorer.Api.DTOs;

public record PolicySearchResult(Guid Id, string PartyName, string PartyAbbreviation, string ManifestoTitle,
    int ElectionYear, string Title, string Topic, string Summary, string FullText, string? SourceUrl,
    string? SourceName, string? PageReference, string? EvidenceQuote, string ExtractionMethod,
    string VerificationStatus, decimal? Confidence);
