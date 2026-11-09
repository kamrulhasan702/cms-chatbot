using System.Text.Json;
using ManualChatbot.Models;
using Microsoft.Extensions.Options;

namespace ManualChatbot.Services;

public sealed class SearchService(KnowledgeStore store, IOptions<KnowledgeOptions> settings, IOptions<AiOptions> ai)
{
    private Dictionary<string, float[]> vectors = [];
    public bool HasEmbeddings => vectors.Count == store.Chunks.Count && vectors.Count > 0;
    public async Task LoadVectorsAsync(CancellationToken ct = default)
    {
        var path = store.AbsolutePath(settings.Value.EmbeddingCachePath);
        if (!File.Exists(path)) return;
        var cache = JsonSerializer.Deserialize<EmbeddingCache>(await File.ReadAllTextAsync(path, ct), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (cache?.CorpusHash == store.CorpusHash && cache.Model == ai.Value.EmbeddingModel &&
            store.Chunks.All(c => cache.Vectors.TryGetValue(c.Id, out var v) && v.Length > 0)) vectors = cache.Vectors;
    }
    public List<SearchHit> FindHeading(string question)
    {
        if (question.Length > 250) return [];
        var normalized = System.Text.RegularExpressions.Regex.Replace(question, @"\b(?:a|an|the)\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var key = KnowledgeStore.HeadingKey(normalized);
        if (key.Length < 6) return [];
        var pages = store.Headings.Where(h => h.Key == key).Select(h => h.Page).ToHashSet();
        return store.Chunks.Where(c => pages.Contains(c.Page)).Take(10).Select(c => new SearchHit(c, 100)).ToList();
    }
    public List<SearchHit> Search(string query, float[]? queryVector = null)
    {
        var tokens = KnowledgeStore.Tokens(query).Distinct().ToArray();
        var docs = store.Chunks.Select(c => (Chunk: c, Tokens: KnowledgeStore.Tokens(c.Text))).ToArray();
        var averageLength = docs.Length == 0 ? 1 : Math.Max(1, docs.Average(d => d.Tokens.Length));
        var frequency = tokens.ToDictionary(t => t, t => docs.Count(d => d.Tokens.Contains(t)));
        var scored = new List<SearchHit>();
        foreach (var doc in docs)
        {
            double keyword = 0;
            foreach (var token in tokens)
            {
                var count = doc.Tokens.Count(t => t == token);
                if (count == 0) continue;
                var idf = Math.Log(1 + (docs.Length - frequency[token] + .5) / (frequency[token] + .5));
                keyword += idf * (count * 2.2) / (count + 1.2 * (.25 + .75 * doc.Tokens.Length / averageLength));
            }
            double? similarity = queryVector is not null && vectors.TryGetValue(doc.Chunk.Id, out var v)
                ? Cosine(queryVector, v) : null;
            // Thresholds are retrieval heuristics; the answerability gate still checks evidence.
            if (keyword < settings.Value.MinimumKeywordScore && similarity is not >= .35) continue;
            var score = similarity is not null ? keyword + Math.Max(0, similarity.Value) * 3 : keyword;
            scored.Add(new(doc.Chunk, score));
        }
        return scored.OrderByDescending(h => h.Score).Take(Math.Clamp(settings.Value.TopK, 1, 10)).ToList();
    }
    public static double Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;
        double dot = 0, aa = 0, bb = 0;
        for (var i = 0; i < a.Length; i++) { dot += (double)a[i] * b[i]; aa += (double)a[i] * a[i]; bb += (double)b[i] * b[i]; }
        return aa == 0 || bb == 0 ? 0 : dot / Math.Sqrt(aa * bb);
    }
}
