using System.Security.Cryptography;
using System.Text;
using ManifestoExplorer.Api.Data;
using ManifestoExplorer.Api.Models;
using ManifestoExplorer.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManifestoExplorer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ManifestosController(AppDbContext db, ManifestoExtractor extractor, IConfiguration config, IWebHostEnvironment env) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var data = await db.Manifestos.AsNoTracking().OrderByDescending(m => m.ElectionYear)
            .Select(m => new { m.Id, m.Title, m.ElectionYear, DocumentUrl = m.DocumentUrl ?? (m.StoredFileName != null ? $"/api/manifestos/{m.Id}/document" : null), m.SourceName, m.PublishedAt,
                Party = m.Party!.Name, Abbreviation = m.Party.Abbreviation,
                VerifiedPolicyCount = m.Policies.Count(p => p.VerificationStatus == "Verified") }).ToListAsync(ct);
        return Ok(data);
    }

    [HttpGet("{id:guid}/document")]
    public async Task<IActionResult> GetDocument(Guid id, CancellationToken ct)
    {
        var manifesto = await db.Manifestos.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);
        if (manifesto?.StoredFileName is null) return NotFound();
        var directory = Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Manifestos:StoragePath"] ?? "App_Data/manifestos"));
        var path = Path.GetFullPath(Path.Combine(directory, manifesto.StoredFileName));
        if (!path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, "application/pdf", enableRangeProcessing: true);
    }

    [HttpGet("ingestion/pending")]
    public async Task<IActionResult> GetPending(CancellationToken ct)
    {
        if (!Authorized()) return Unauthorized(new { message = "Valid ingestion API key required." });
        var items = await db.Policies.AsNoTracking().Where(p => p.VerificationStatus == "NeedsReview")
            .OrderBy(p => p.CreatedAt).Select(p => new { p.Id, p.Title, p.Topic, p.Summary, p.FullText, p.EvidenceQuote,
                p.PageReference, p.ExtractionMethod, p.Confidence, Manifesto = p.Manifesto!.Title,
                Party = p.Manifesto.Party!.Name, DocumentUrl = p.Manifesto.DocumentUrl ?? (p.Manifesto.StoredFileName != null ? $"/api/manifestos/{p.Manifesto.Id}/document" : null) }).ToListAsync(ct);
        return Ok(new { count = items.Count, items });
    }

    // Ingestion API, deliberately separate from the public React experience. Protect with a server-side API key.
    [HttpPost("upload")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] Guid partyId,
        [FromForm] string title, [FromForm] int electionYear, [FromForm] string? sourceUrl, CancellationToken ct)
    {
        if (!Authorized()) return Unauthorized(new { message = "Valid ingestion API key required." });
        if (file is null || file.Length == 0 || !Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Upload a non-empty PDF file." });
        if (file.Length > 25_000_000) return BadRequest(new { message = "PDF must be 25 MB or smaller." });
        if (string.IsNullOrWhiteSpace(title) || title.Length > 250 || electionYear < 1994 || electionYear > DateTime.UtcNow.Year + 1)
            return BadRequest(new { message = "Provide a valid title and South African election year." });
        if (!string.IsNullOrWhiteSpace(sourceUrl) && (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            return BadRequest(new { message = "Source URL must be a valid HTTPS URL." });
        var party = await db.Parties.FirstOrDefaultAsync(p => p.Id == partyId, ct);
        if (party is null) return NotFound(new { message = "Party not found. Add the party record before uploading." });

        var directory = Path.Combine(env.ContentRootPath, config["Manifestos:StoragePath"] ?? "App_Data/manifestos");
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $"{Guid.NewGuid():N}.pdf");
        try
        {
            await using (var stream = System.IO.File.Create(tempPath)) await file.CopyToAsync(stream, ct);
            string hash;
            await using (var stream = System.IO.File.OpenRead(tempPath)) hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
            if (await db.Manifestos.AnyAsync(m => m.ContentSha256 == hash, ct)) return Conflict(new { message = "This exact PDF has already been ingested." });

            IReadOnlyList<ExtractedPage> pages;
            try { pages = await extractor.ReadPdfAsync(tempPath, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { return BadRequest(new { message = "Could not read this PDF. It may be encrypted, damaged or image-only; OCR is not configured.", detail = ex.Message }); }
            if (pages.Count == 0 || pages.Sum(p => p.Text.Length) < 100) return BadRequest(new { message = "No selectable text was found. This version requires text-based PDFs; OCR support can be added separately." });

            var candidates = await extractor.ExtractAsync(pages, ct);
            var savedName = $"{Guid.NewGuid():N}.pdf";
            var savedPath = Path.Combine(directory, savedName);
            System.IO.File.Move(tempPath, savedPath);
            var manifesto = new Manifesto { PartyId = partyId, Title = title.Trim(), ElectionYear = electionYear,
                DocumentUrl = sourceUrl, SourceName = Path.GetFileName(file.FileName), StoredFileName = savedName, ContentSha256 = hash };
            foreach (var item in candidates)
                manifesto.Policies.Add(new Policy { Title = item.Title, Topic = item.Topic, Summary = item.Summary,
                    FullText = item.FullText, EvidenceQuote = item.EvidenceQuote, PageReference = item.PageReference,
                    ExtractionMethod = item.ExtractionMethod, Confidence = item.Confidence, VerificationStatus = "NeedsReview" });
            db.Manifestos.Add(manifesto);
            await db.SaveChangesAsync(ct);
            return Ok(new { manifestoId = manifesto.Id, manifesto.Title, party = party.Name, pages = pages.Count,
                candidateCount = candidates.Count, status = "NeedsReview", candidates = manifesto.Policies.Select(p => new { p.Id, p.Title, p.Topic, p.Summary, p.FullText, p.EvidenceQuote, p.PageReference, p.ExtractionMethod, p.Confidence, p.VerificationStatus }),
                message = "Candidates saved as drafts. They are not visible in public search until verified." });
        }
        finally { if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath); }
    }

    [HttpPatch("policies/{policyId:guid}/review")]
    public async Task<IActionResult> ReviewCandidate(Guid policyId, [FromBody] PolicyReviewRequest request, CancellationToken ct)
    {
        if (!Authorized()) return Unauthorized(new { message = "Valid ingestion API key required." });
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 250 ||
            string.IsNullOrWhiteSpace(request.Topic) || request.Topic.Length > 100 ||
            string.IsNullOrWhiteSpace(request.Summary) || request.Summary.Length > 1500 ||
            string.IsNullOrWhiteSpace(request.EvidenceQuote) || request.EvidenceQuote.Length > 700)
            return BadRequest(new { message = "Provide a title, topic, neutral summary and evidence quote within the documented limits." });
        var policy = await db.Policies.FirstOrDefaultAsync(p => p.Id == policyId, ct);
        if (policy is null) return NotFound();
        if (!policy.FullText.Contains(request.EvidenceQuote, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "The evidence quote must be present in the extracted source passage." });
        policy.Title = request.Title.Trim();
        policy.Topic = request.Topic.Trim();
        policy.Summary = request.Summary.Trim();
        policy.EvidenceQuote = request.EvidenceQuote.Trim();
        policy.VerificationStatus = "NeedsReview";
        policy.VerifiedAt = null;
        await db.SaveChangesAsync(ct);
        return Ok(new { policy.Id, policy.Title, policy.Topic, policy.Summary, policy.EvidenceQuote, policy.VerificationStatus });
    }

    [HttpPatch("policies/{policyId:guid}/verification")]
    public async Task<IActionResult> SetVerification(Guid policyId, [FromBody] VerificationRequest request, CancellationToken ct)
    {
        if (!Authorized()) return Unauthorized(new { message = "Valid ingestion API key required." });
        if (request.Status is not ("Verified" or "NeedsReview" or "Rejected")) return BadRequest(new { message = "Status must be Verified, NeedsReview or Rejected." });
        var policy = await db.Policies.FirstOrDefaultAsync(p => p.Id == policyId, ct);
        if (policy is null) return NotFound();
        if (request.Status == "Verified" && (string.IsNullOrWhiteSpace(policy.EvidenceQuote) || !policy.FullText.Contains(policy.EvidenceQuote, StringComparison.OrdinalIgnoreCase) || policy.Summary.StartsWith("Draft summary required", StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { message = "Cannot verify a candidate without an evidence quote contained in the extracted source text." });
        policy.VerificationStatus = request.Status;
        policy.VerifiedAt = request.Status == "Verified" ? DateTimeOffset.UtcNow : null;
        await db.SaveChangesAsync(ct);
        return Ok(new { policy.Id, policy.VerificationStatus, policy.VerifiedAt });
    }

    private bool Authorized()
    {
        var expected = config["Ingestion:ApiKey"];
        var supplied = Request.Headers["X-Ingestion-Key"].ToString();
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(supplied)) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));
    }
}

public record VerificationRequest(string Status);
public record PolicyReviewRequest(string Title, string Topic, string Summary, string EvidenceQuote);
