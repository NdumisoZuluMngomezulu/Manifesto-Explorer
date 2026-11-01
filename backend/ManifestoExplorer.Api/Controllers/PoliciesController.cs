using ManifestoExplorer.Api.Data;
using ManifestoExplorer.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManifestoExplorer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoliciesController(AppDbContext db) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? keyword, [FromQuery] string? party,
        [FromQuery] string? topic, [FromQuery] int? year, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(keyword) && string.IsNullOrWhiteSpace(topic) && string.IsNullOrWhiteSpace(party))
            return BadRequest(new { message = "Enter a keyword, party or topic to search." });
        if (keyword?.Length > 120 || party?.Length > 150 || topic?.Length > 100)
            return BadRequest(new { message = "Search filters are too long." });

        var query = db.Policies.AsNoTracking().Where(p => p.VerificationStatus == "Verified")
            .Include(p => p.Manifesto)!.ThenInclude(m => m!.Party).AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = $"%{keyword.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Title, term) || EF.Functions.ILike(p.Topic, term)
                || EF.Functions.ILike(p.Summary, term) || EF.Functions.ILike(p.FullText, term) || EF.Functions.ILike(p.EvidenceQuote!, term));
        }
        if (!string.IsNullOrWhiteSpace(topic)) query = query.Where(p => EF.Functions.ILike(p.Topic, $"%{topic.Trim()}%"));
        if (!string.IsNullOrWhiteSpace(party)) query = query.Where(p => EF.Functions.ILike(p.Manifesto!.Party!.Name, $"%{party.Trim()}%")
            || EF.Functions.ILike(p.Manifesto.Party.Abbreviation!, $"%{party.Trim()}%"));
        if (year.HasValue) query = query.Where(p => p.Manifesto!.ElectionYear == year.Value);

        var results = await query.OrderBy(p => p.Manifesto!.Party!.Name).ThenBy(p => p.Topic).Take(150)
            .Select(p => new PolicySearchResult(p.Id, p.Manifesto!.Party!.Name, p.Manifesto.Party.Abbreviation ?? "",
                p.Manifesto.Title, p.Manifesto.ElectionYear, p.Title, p.Topic, p.Summary, p.FullText,
                p.Manifesto.DocumentUrl ?? (p.Manifesto.StoredFileName != null ? $"/api/manifestos/{p.Manifesto.Id}/document" : null), p.Manifesto.SourceName, p.PageReference, p.EvidenceQuote,
                p.ExtractionMethod, p.VerificationStatus, p.Confidence)).ToListAsync(ct);
        return Ok(new { count = results.Count, results, note = "Only human-verified policies are shown." });
    }

    [HttpGet("compare")]
    public async Task<IActionResult> Compare([FromQuery] string topic, [FromQuery] int? year, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(topic)) return BadRequest(new { message = "A topic is required." });
        var query = db.Policies.AsNoTracking().Where(p => p.VerificationStatus == "Verified" && EF.Functions.ILike(p.Topic, $"%{topic.Trim()}%"))
            .Include(p => p.Manifesto)!.ThenInclude(m => m!.Party).AsQueryable();
        if (year.HasValue) query = query.Where(p => p.Manifesto!.ElectionYear == year.Value);
        var results = await query.OrderBy(p => p.Manifesto!.Party!.Name)
            .Select(p => new PolicySearchResult(p.Id, p.Manifesto!.Party!.Name, p.Manifesto.Party.Abbreviation ?? "", p.Manifesto.Title,
                p.Manifesto.ElectionYear, p.Title, p.Topic, p.Summary, p.FullText, p.Manifesto.DocumentUrl ?? (p.Manifesto.StoredFileName != null ? $"/api/manifestos/{p.Manifesto.Id}/document" : null),
                p.Manifesto.SourceName, p.PageReference, p.EvidenceQuote, p.ExtractionMethod, p.VerificationStatus, p.Confidence))
            .Take(300).ToListAsync(ct);
        return Ok(new { topic, count = results.Count, results });
    }
}
