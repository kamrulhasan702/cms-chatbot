using System.Text.RegularExpressions;

namespace ManualChatbot.Services;

// Deterministic search normalization: no LLM calls, no external translation API.
public static class LocalQueryService
{
    private static readonly (string Pattern, string Replacement)[] Terms = [
        (@"\b(kivabe|kibhabe|kemne|ki vabe|how)\b|কীভাবে|কিভাবে", "how"),
        (@"\b(chobi|pic|picture)\b|ছবি", "image"),
        (@"\b(korbo|korte|koro|korben|kora|korbo)\b|করব|করবো|করতে|করুন", ""),
        (@"\b(kothay|koi)\b|কোথায়|কোথায়", "where"),
        (@"\b(khujbo|khuje|search)\b|খুঁজে|খুঁজবো", "search"),
        (@"\b(debo|dibo|dite|add)\b|যোগ|দিতে", "add"),
        (@"\b(khobor|news)\b|নিউজ|খবর", "news"),
        (@"\b(prokash|publish)\b|প্রকাশ", "publish"),
        (@"\b(schedule|shidul|sidul)\b|শিডিউল|সময়সূচি", "schedule"),
        (@"\b(upload|aplod)\b|আপলোড", "upload"),
        (@"\b(gallery|gelari)\b|গ্যালারি", "gallery"),
        (@"\b(link|lingk)\b|লিংক", "link"),
        (@"\b(category|catagory)\b|ক্যাটাগরি", "category"),
        (@"\b(draft|draf)\b|ড্রাফট", "draft"),
        (@"\b(login|log in)\b|লগইন", "login"),
        (@"\b(table|tebil)\b|টেবিল", "table"),
        (@"\b(video|vidio)\b|ভিডিও", "video"),
        (@"\b(image|img)\b|ইমেজ", "image"),
        (@"\b(ki|ami|amr|amar|eta|eita|ta|theke|theke|e|a|er|te|ta|jonno|hobe|hoy)\b|আমি|আমার|এর|এটা|কি|কী", "")
    ];
    public static bool IsCmsQuestion(string question, bool hasImage = false)
    {
        // Local conservative scope filter; model still checks scope against evidence.
        if (Regex.IsMatch(question, @"\b(facebook|instagram|whatsapp|wordpress|shopify|weather|politics|joke|stock market|bitcoin)\b", RegexOptions.IgnoreCase)) return false;
        if (Regex.IsMatch(question, @"\b(ignore (the |this )?manual|ignore previous|forget (the |this )?manual)\b", RegexOptions.IgnoreCase)) return false;
        if (hasImage) return true;
        var normalized = Normalize(question);
        return Regex.IsMatch(normalized, @"\b(cms|on.air|news|story|article|image|photo|video|gallery|table|link|category|subcategory|author|tag|login|dashboard|publish|schedule|draft|editor|upload|featured|sorting|snippet|collection|menu|seo|headline|caption|alignment|pdf|embed|filter|lock|history|preview|media|user|permission|gold price|point table|trivia|match|summary|rich.text|content|insert)\b", RegexOptions.IgnoreCase);
    }
    public static string Normalize(string query)
    {
        var text = query;
        foreach (var (pattern, replacement) in Terms)
            text = Regex.Replace(text, pattern, " " + replacement + " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
}
