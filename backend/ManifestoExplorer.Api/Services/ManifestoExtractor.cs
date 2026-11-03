using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig;

namespace ManifestoExplorer.Api.Services;

public record ExtractedPage(int PageNumber, string Text);
public record CandidatePolicy(string Title, string Topic, string Summary, string FullText,
    string? EvidenceQuote, string? PageReference, string ExtractionMethod, decimal? Confidence);

public class ManifestoExtractor(HttpClient http, IConfiguration config, ILogger<ManifestoExtractor> logger)
{
    private static readonly Dictionary<string, string[]> TopicTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Immigration"] = ["immigration", "migrant", "migration", "asylum", "refugee", "border", "undocumented"],
        ["Social grants"] = ["social grant", "child support grant", "old age grant", "disability grant", "grant increase", "social security"],
        ["Policing"] = ["police", "policing", "saps", "crime", "law enforcement", "community safety", "criminal justice"],
        ["Education"] = ["education", "school", "teacher", "university", "student", "skills training"],
        ["Healthcare"] = ["healthcare", "health care", "hospital", "clinic", "nhi", "medical"],
        ["Jobs"] = ["employment", "unemployment", "job creation", "jobs", "workers", "small business"],
        ["Housing"] = ["housing", "houses", "informal settlement", "land reform", "spatial planning"],
        ["Corruption"] = ["corruption", "tender", "procurement", "accountability", "state capture"],
        ["Economy"] = ["economy", "economic growth", "tax", "fiscal", "inflation", "investment"],
        ["Energy"] = ["electricity", "energy", "load shedding", "renewable", "Eskom"],
        ["Transport"] = ["transport", "rail", "prasa", "roads", "public transit"]
    };

    public async Task<IReadOnlyList<ExtractedPage>> ReadPdfAsync(string path, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            using var document = PdfDocument.Open(path);
            return (IReadOnlyList<ExtractedPage>)document.GetPages()
                .Select(p => new ExtractedPage(p.Number, Normalize(ContentOrderTextExtractor.GetText(p))))
                .Where(p => !string.IsNullOrWhiteSpace(p.Text)).ToList();
        }, ct);
    }

    public async Task<List<CandidatePolicy>> ExtractAsync(IReadOnlyList<ExtractedPage> pages, CancellationToken ct)
    {
        var keywordCandidates = ExtractByKeywords(pages);
        var aiCandidates = await ExtractWithAiAsync(pages, ct);
        // AI enriches the deterministic candidate set; deduplicate by topic and evidence text.
        var merged = new List<CandidatePolicy>(keywordCandidates);
        foreach (var candidate in aiCandidates)
        {
            var duplicate = merged.Any(x => x.Topic.Equals(candidate.Topic, StringComparison.OrdinalIgnoreCase)
                && SimilarityKey(x.EvidenceQuote ?? x.FullText) == SimilarityKey(candidate.EvidenceQuote ?? candidate.FullText));
            if (!duplicate) merged.Add(candidate);
        }
        return merged.OrderBy(x => x.Topic).ThenBy(x => x.PageReference).ToList();
    }

    private static List<CandidatePolicy> ExtractByKeywords(IReadOnlyList<ExtractedPage> pages)
    {
        var found = new List<CandidatePolicy>();
        foreach (var page in pages)
        {
            var paragraphs = Regex.Split(page.Text, @"\n\s*\n|(?<=[.!?])\s{2,}");
            foreach (var paragraph in paragraphs)
            {
                var text = Normalize(paragraph);
                if (text.Length < 45) continue;
                foreach (var topic in TopicTerms)
                {
                    if (!topic.Value.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase))) continue;
                    var quote = text.Length > 700 ? text[..700] : text;
                    found.Add(new CandidatePolicy(
                        Title: BuildTitle(topic.Key, text), Topic: topic.Key,
                        Summary: "Draft summary required: review the original passage and write a neutral summary before publication.",
                        FullText: text, EvidenceQuote: quote, PageReference: $"Page {page.PageNumber}",
                        ExtractionMethod: "Keyword", Confidence: null));
                }
            }
        }
        return found.GroupBy(x => (x.Topic, Key: SimilarityKey(x.FullText))).Select(g => g.First()).ToList();
    }

    private async Task<List<CandidatePolicy>> ExtractWithAiAsync(IReadOnlyList<ExtractedPage> pages, CancellationToken ct)
    {
        var endpoint = config["AiExtraction:Endpoint"];
        var apiKey = config["AiExtraction:ApiKey"];
        var model = config["AiExtraction:Model"] ?? "gpt-4o-mini";
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey)) return [];

        var output = new List<CandidatePolicy>();
        foreach (var page in pages.Take(Math.Clamp(config.GetValue<int?>("AiExtraction:MaxPages") ?? 80, 1, 500)))
        {
            ct.ThrowIfCancellationRequested();
            if (page.Text.Length < 80) continue;
            var content = page.Text.Length > 12000 ? page.Text[..12000] : page.Text;
            var prompt = "You are extracting political policy claims for a neutral civic-information database. " +
                "Only report explicit proposals, commitments, or stated positions present in the supplied source. " +
                "Do not infer, endorse, rank, or invent. Return JSON only: {\"policies\":[{\"title\":string,\"topic\":string,\"summary\":string,\"evidenceQuote\":string,\"confidence\":number}]}. " +
                "Summary must be neutral and concise. evidenceQuote must be an exact contiguous quote copied from the text, max 500 chars. " +
                "Use topic labels such as Immigration, Social grants, Policing, Education, Healthcare, Jobs, Housing, Corruption, Economy, Energy, Transport, or Other. " +
                "If there is no explicit policy, return an empty array. Source page: " + page.PageNumber + "\n\nSOURCE TEXT:\n" + content;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = JsonContent.Create(new { model, temperature = 0, response_format = new { type = "json_object" }, messages = new[] { new { role = "user", content = prompt } } });
                using var response = await http.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode) { logger.LogWarning("AI extraction returned {StatusCode}", response.StatusCode); continue; }
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var raw = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                if (string.IsNullOrWhiteSpace(raw)) continue;
                using var parsed = JsonDocument.Parse(raw);
                if (!parsed.RootElement.TryGetProperty("policies", out var policies) || policies.ValueKind != JsonValueKind.Array) continue;
                foreach (var item in policies.EnumerateArray())
                {
                    string Get(string key) => item.TryGetProperty(key, out var value) ? value.GetString() ?? "" : "";
                    var quote = Get("evidenceQuote");
                    // Never persist an AI claim unless its purported evidence is actually present in this page.
                    if (quote.Length < 20 || !page.Text.Contains(quote, StringComparison.OrdinalIgnoreCase)) continue;
                    var title = Get("title"); var topic = Get("topic"); var summary = Get("summary");
                    if (title.Length == 0 || topic.Length == 0 || summary.Length == 0) continue;
                    decimal? confidence = item.TryGetProperty("confidence", out var c) && c.TryGetDecimal(out var n) ? Math.Clamp(n, 0m, 1m) : null;
                    output.Add(new CandidatePolicy(title[..Math.Min(250, title.Length)], topic[..Math.Min(100, topic.Length)],
                        summary[..Math.Min(1500, summary.Length)], quote, quote, $"Page {page.PageNumber}", "AI+Keyword", confidence));
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            { logger.LogWarning(ex, "AI extraction failed for page {Page}", page.PageNumber); }
        }
        return output;
    }

    private static string BuildTitle(string topic, string text)
    {
        var words = Regex.Split(text, @"\s+").Take(9);
        return $"{topic}: {string.Join(' ', words)}"[..Math.Min(250, ($"{topic}: {string.Join(' ', words)}").Length)];
    }
    private static string SimilarityKey(string text) => Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9]", "");
    private static string Normalize(string text) => Regex.Replace(text.Replace('\r', '\n'), @"[\t ]+", " ").Trim();
}
