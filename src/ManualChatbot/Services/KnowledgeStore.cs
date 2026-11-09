using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ManualChatbot.Models;
using Microsoft.Extensions.Options;

namespace ManualChatbot.Services;

public sealed partial class KnowledgeStore(IHostEnvironment host, IOptions<KnowledgeOptions> options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public IReadOnlyList<KnowledgeChunk> Chunks { get; private set; } = [];
    public IReadOnlyList<ManualHeading> Headings { get; private set; } = [];
    public string CorpusHash { get; private set; } = "";
    public string Title { get; private set; } = "Manual";
    public string AbsolutePath(string relative) => Path.GetFullPath(relative, host.ContentRootPath);
    public async Task LoadAsync(CancellationToken ct = default)
    {
        var bytes = await File.ReadAllBytesAsync(AbsolutePath(options.Value.SourcePath), ct);
        var document = JsonSerializer.Deserialize<ManualDocument>(bytes, Json) ?? throw new InvalidDataException("Manual data is invalid.");
        CorpusHash = Convert.ToHexString(SHA256.HashData(bytes));
        Title = document.Title;
        Chunks = BuildChunks(document);
        Headings = BuildHeadings(document);
        if (Chunks.Count == 0) throw new InvalidDataException("No text found. Import readable text or OCR first.");
    }
    public static IReadOnlyList<KnowledgeChunk> BuildChunks(ManualDocument document)
    {
        var result = new List<KnowledgeChunk>();
        foreach (var page in document.Pages)
        {
            var text = Regex.Replace(page.Text + "\n" + page.OcrText, @"[ \t]+", " ").Trim();
            if (text.Length < 45) continue;
            var words = Regex.Split(text, @"\s+");
            const int size = 210, overlap = 35;
            for (var start = 0; start < words.Length; start += size - overlap)
            {
                var body = string.Join(' ', words.Skip(start).Take(size));
                if (body.Length < 45) break;
                result.Add(new($"c{result.Count + 1:D4}", body, page.Page));
                if (start + size >= words.Length) break;
            }
        }
        return result;
    }
    public static string HeadingKey(string text) => Regex.Replace(text.Normalize(NormalizationForm.FormKC).ToLowerInvariant(), @"[^\p{L}\p{M}\p{N}]", "");
    public static IReadOnlyList<ManualHeading> BuildHeadings(ManualDocument document)
    {
        var headings = new List<ManualHeading>();
        foreach (var page in document.Pages)
        {
            var lines = page.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var title = "";
            foreach (var line in lines.Take(3))
            {
                if (line.StartsWith('•') || line.StartsWith('+') || line.Length > 180) break;
                title += " " + line;
                var key = HeadingKey(title);
                if (key.Length is >= 6 and <= 180)
                {
                    headings.Add(new(key, page.Page));
                    var withoutSectionNumber = Regex.Replace(key, @"\d+$", "");
                    if (withoutSectionNumber.Length >= 6 && withoutSectionNumber != key)
                        headings.Add(new(withoutSectionNumber, page.Page));
                }
            }
        }
        return headings.Distinct().ToArray();
    }
    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex Words();
    private static readonly HashSet<string> StopWords = new("a an the how do does i to of in on at for from is are can my me please what where and or it this that with be by into you your tell about should would could".Split(' '));
    public static string[] Tokens(string text) => Words().Matches(text.ToLowerInvariant())
        .Select(m => Stem(m.Value)).Where(w => w.Length > 1 && !StopWords.Contains(w)).ToArray();
    private static string Stem(string text)
    {
        if (text.EndsWith("ing") && text.Length > 6) return text[..^3];
        if (text.EndsWith("ies") && text.Length > 5) return text[..^3] + "y";
        if (text.EndsWith('s') && !text.EndsWith("ss") && text.Length > 4) return text[..^1];
        return text;
    }
}
