namespace ManualChatbot.Models;

public sealed class AiOptions
{
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-4.1-mini";
    public string VisionModel { get; set; } = "";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public bool UseEmbeddings { get; set; }
    public bool UseJsonMode { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 25;
    public int MaxOutputTokens { get; set; } = 900;
}
public sealed class KnowledgeOptions
{
    public string SourcePath { get; set; } = "Knowledge/manual.pages.json";
    public string EmbeddingCachePath { get; set; } = "App_Data/embeddings.json";
    public int TopK { get; set; } = 6;
    public double MinimumKeywordScore { get; set; } = .65;
}
public sealed record HistoryMessage(string Role, string Content);
public sealed record ChatRequest(string Message, List<HistoryMessage>? History = null, string? ImageDataUrl = null);
public sealed record ChatResponse(string Answer, string Language, bool UsedFallback, IReadOnlyList<ManualImage>? Images = null);
public sealed record ManualImage(string Url, string Caption);
public sealed record FigureEntry(string Id, int Page, int[] RelatedPages, string Caption, string FileName);
public sealed record FigureManifest(string CorpusHash, List<FigureEntry> Images);
public sealed record QueryPlan(string Language, string SearchQuery, bool InScope = false);
public sealed record AnswerDraft(bool Answerable, string Answer, string[] EvidenceIds);
public sealed record ManualPage(int Page, string Text, string OcrText = "", string Source = "text");
public sealed record ManualDocument(string DocumentId, string Title, List<ManualPage> Pages);
public sealed record KnowledgeChunk(string Id, string Text, int Page);
public sealed record SearchHit(KnowledgeChunk Chunk, double Score);
public sealed record ManualHeading(string Key, int Page);
public sealed record EmbeddingCache(string CorpusHash, string Model, Dictionary<string, float[]> Vectors);
public sealed class AiException(string code, string message, int status = 502) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public sealed record ScreenshotObservation(bool Readable, bool IsCmsScreen, string Observation, string SearchQuery);
