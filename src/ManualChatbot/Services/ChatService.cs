using System.Text.RegularExpressions;
using ManualChatbot.Models;
using Microsoft.Extensions.Options;

namespace ManualChatbot.Services;

public sealed class ChatService(IAiService ai, SearchService search, IOptions<AiOptions> options, FigureService? figures = null)
{
    public async Task<ChatResponse> ReplyAsync(ChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 2500)
            throw new ArgumentException("Write a question between 1 and 2,500 characters.");
        var hasImage = request.ImageDataUrl is not null;
        if (hasImage) ScreenshotInput.Validate(request.ImageDataUrl!);
        var smallTalk = hasImage ? null : SmallTalkService.TryReply(request.Message);
        if (smallTalk is not null) return smallTalk;
        var history = (request.History ?? []).TakeLast(6)
            .Where(m => m is not null && (m.Role is "user" or "assistant") && !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => m with { Content = m.Content[..Math.Min(m.Content.Length, 1500)] }).ToArray();
        var question = request.Message.Trim();
        var language = LanguageService.Detect(question);
        // For this documented procedure, keep steps and UI labels identical across languages.
        if (!hasImage && ManualProcedureService.TryAnswer(question, language, search, figures) is { } stableAnswer)
            return stableAnswer;
        string? observedQuery = null;
        if (hasImage)
        {
            var screen = await ai.ObserveAsync(question, request.ImageDataUrl!, ct);
            if (!screen.IsCmsScreen && screen.Readable) return Fallback(language);
            if (!screen.Readable || string.IsNullOrWhiteSpace(screen.Observation))
                return new(language == "bn" ? "ছবিটা পরিষ্কার বোঝা যাচ্ছে না। প্রাসঙ্গিক অংশের পরিষ্কার screenshot দিয়ে আবার প্রশ্ন করুন।" :
                    language == "banglish" ? "Chobita porishkar bojha jacche na. Relevant ongshotar clear screenshot diye abar question korun." :
                    "I cannot read the screenshot clearly. Please upload a clearer screenshot of the relevant area and ask again.", language, false);
            observedQuery = screen.SearchQuery;
            question = System.Text.Json.JsonSerializer.Serialize(new { userQuestion = question,
                screenshotObservation = screen.Observation,
                instruction = "Use the screenshot only as current screen context. Give next steps only from the manual." });
        }
        if (!LocalQueryService.IsCmsQuestion(request.Message, hasImage)) return Fallback(language);
        // Text-only requests use local retrieval, then exactly one completion request.
        // Screenshot requests need a separate vision call before this step.
        var searchQuery = LocalQueryService.Normalize(hasImage ? (observedQuery ?? request.Message) : question);
        var hits = search.FindHeading(question);
        if (hits.Count == 0 && !hasImage) hits = search.FindHeading(searchQuery);
        if (hits.Count == 0) hits = search.Search(searchQuery);
        var plan = new QueryPlan(language, searchQuery, hits.Count > 0);
        var answerQuestion = question;
        if (hits.Count > 0 && search.FindHeading(question).Count > 0)
            answerQuestion = "The user supplied a manual section heading: " + question +
                "\nExplain only the documented procedure or feature.";
        if (hits.Count == 0) return Fallback(plan.Language);
        var draft = await ai.AnswerAsync(answerQuestion, plan.Language, hits, ct);
        var availableIds = hits.Select(h => h.Chunk.Id).ToHashSet();
        if (!draft.Answerable || string.IsNullOrWhiteSpace(draft.Answer) ||
            draft.EvidenceIds is null || draft.EvidenceIds.Length == 0 ||
            draft.EvidenceIds.Any(id => !availableIds.Contains(id))) return Fallback(plan.Language);
        var answer = CleanAnswer(draft.Answer);
        if (!LocalAnswerValidator.IsSafe(answer, hits, draft.EvidenceIds)) return Fallback(plan.Language);
        if (string.IsNullOrWhiteSpace(answer)) return Fallback(plan.Language);
        return new(answer, plan.Language, false, figures?.Select(hits, draft.EvidenceIds, request.Message, answer) ?? []);
    }
    public static ChatResponse Fallback(string language) => new(LanguageService.Fallback(language), language, true);
    public static string CleanAnswer(string answer)
    {
        var cleaned = Regex.Replace(answer, @"\[(?:source|chunk|page|p\.)[^\]]*\]|\((?:page|p\.)\s*\d+(?:\s*[-–]\s*\d+)?\)", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\bc\d{4,}\b", "");
        return cleaned.Trim()[..Math.Min(cleaned.Trim().Length, 7000)];
    }
}
