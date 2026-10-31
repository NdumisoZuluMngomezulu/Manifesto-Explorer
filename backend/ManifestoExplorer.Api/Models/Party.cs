namespace ManifestoExplorer.Api.Models;

public class Party
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Abbreviation { get; set; }
    public string? Description { get; set; }
    public string? WebsiteUrl { get; set; }
    public List<Manifesto> Manifestos { get; set; } = [];
}
