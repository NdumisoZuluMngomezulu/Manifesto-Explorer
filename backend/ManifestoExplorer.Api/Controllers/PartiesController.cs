using System.Reflection.Metadata;
using ManifestoExplorer.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManifestoExplorer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PartiesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var parties = await db.Parties.AsNoTracking()
            .Select(p => new { p.Id, p.Name, p.Abbreviation, p.Description, p.WebsiteUrl,
                ManifestoCount = p.Manifestos.Count, VerifiedPolicyCount = p.Manifestos.SelectMany(m => m.Policies).Count(x => x.VerificationStatus == "Verified") })
            .OrderBy(p => p.Name).ToListAsync(ct);
        return Ok(parties);    
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var party = await db.Parties.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new { p.Id, p.Name, p.Abbreviation, p.Description, p.WebsiteUrl,
                Manifestos = p.Manifestos.OrderByDescending(m => m.ElectionYear).Select(m => new { m.Id, m.Title, m.ElectionYear, DocumentUrl = m.DocumentUrl ?? (m.StoredFileName != null ? $"/api/manifestos/{m.Id}/document" : null), m.SourceName, m.PublishedAt,
                    Policies = m.Policies.Where(x => x.VerificationStatus == "Verified").Select(x => new { x.Id, x.Title, x.Topic, x.Summary, x.FullText, x.PageReference, x.EvidenceQuote, x.VerificationStatus }) }) })
            .FirstOrDefaultAsync(ct);
        return party is null ? NotFound() : Ok(party);
    }
}