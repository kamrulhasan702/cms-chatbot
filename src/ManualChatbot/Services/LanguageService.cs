using System.Text.RegularExpressions;

namespace ManualChatbot.Services;

public static partial class LanguageService
{
    [GeneratedRegex("[\\u0980-\\u09FF]")]
    private static partial Regex Bengali();
    [GeneratedRegex(@"\b(kivabe|kibhabe|ki|keno|kano|korbo|korte|koro|koren|kore|kora|kori|kortechi|hobe|hoy|hoche|hocche|ase|ache|nai|nei|amar|amr|ami|amake|amk|tumi|apni|eta|eita|oita|kothay|koi|daw|dao|dekhabo|dekhay|bolen|bolo|jonno|chaitesi|chai|banabo|bujhi|bujhao|thake|thakbe|dib[oae]|dibo|dib[eai]|parbo|parchi|koto|gula|gulo|shathe|sathe|tahole)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Banglish();
    public static string Detect(string message) => Bengali().IsMatch(message) ? "bn" : Banglish().IsMatch(message) ? "banglish" : "en";
    public static string Resolve(string message, string predicted)
    {
        var detected = Detect(message);
        return detected != "en" ? detected : predicted is "bn" or "banglish" or "en" ? predicted : "en";
    }
    public static string Fallback(string language) => language switch
    {
        "bn" => "এই বিষয়ে আমার কাছে কোনো তথ্য নেই।",
        "banglish" => "Ei bishoye amar kache kono information nei.",
        _ => "I have no information about this."
    };
}
