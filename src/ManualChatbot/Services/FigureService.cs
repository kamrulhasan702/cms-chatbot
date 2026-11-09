using System.Text.Json;
using System.Text.RegularExpressions;
using ManualChatbot.Models;

namespace ManualChatbot.Services;

public sealed class FigureService(KnowledgeStore store)
{
    private readonly Dictionary<string, FigureEntry> entries = [];
    public async Task LoadAsync(CancellationToken ct = default)
    {
        entries.Clear();
        var path = store.AbsolutePath("Knowledge/figures.json");
        if (!File.Exists(path)) return;
        var manifest = JsonSerializer.Deserialize<FigureManifest>(await File.ReadAllTextAsync(path, ct), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        // Old illustrations must never be attached to a replacement manual.
        if (manifest?.CorpusHash != store.CorpusHash) return;
        foreach (var entry in manifest.Images)
            if (Regex.IsMatch(entry.Id, @"^fig-[a-f0-9]{12}$") && entry.FileName == entry.Id + ".webp" &&
                entry.RelatedPages is not null && File.Exists(store.AbsolutePath("Knowledge/figures/" + entry.FileName)))
                entries.TryAdd(entry.Id, entry);
    }
    public string? FileFor(string id) => entries.TryGetValue(id, out var entry)
        ? store.AbsolutePath("Knowledge/figures/" + entry.FileName) : null;
    public IReadOnlyList<ManualImage> Select(IReadOnlyList<SearchHit> hits, IEnumerable<string> citedIds, string question, string answer)
    {
        var cited = citedIds.ToHashSet();
        var pages = hits.Where(h => cited.Contains(h.Chunk.Id)).Select(h => h.Chunk.Page).ToHashSet();
        var tokens = KnowledgeStore.Tokens(question + " " + answer).ToHashSet();
        return entries.Values.Where(e => e.RelatedPages.Any(pages.Contains))
            .OrderByDescending(e => KnowledgeStore.Tokens(e.Caption).Count(tokens.Contains))
            .ThenByDescending(e => pages.Contains(e.Page))
            .ThenBy(e => e.Page).ThenBy(e => e.Id)
            .Take(3).Select(e => new ManualImage("/api/manual-images/" + e.Id, e.Caption)).ToArray();
    }
}
