using System.Text;
using System.Text.RegularExpressions;
using ManualChatbot.Models;

namespace ManualChatbot.Services;

public static class SmallTalkService
{
    private static readonly HashSet<string> Greetings = new(StringComparer.Ordinal) {
        "hi", "hello", "hey", "hi there", "hello there", "good morning", "good afternoon", "good evening",
        "হাই", "হ্যালো", "হেলো", "হেই", "সুপ্রভাত", "শুভ সকাল", "শুভ সন্ধ্যা",
        "আসসালামু আলাইকুম", "আসসালামুয়ালাইকুম", "সালাম", "assalamualaikum", "assalamu alaikum", "salam", "salaam"
    };
    private static readonly HashSet<string> Goodbyes = new(StringComparer.Ordinal) {
        "bye", "goodbye", "good bye", "bye bye", "see you", "see you later", "take care", "good night",
        "বিদায়", "বিদায়", "বাই", "আবার দেখা হবে", "ভালো থাকবেন", "শুভ রাত্রি", "biday", "bidai", "abar dekha hobe", "bhalo thakben", "valo thakben"
    };
    private static readonly HashSet<string> Acknowledgements = new(StringComparer.Ordinal) {
        "ok", "okay", "okey", "alright", "all right", "got it", "understood", "sure", "yes", "no", "yep", "nope",
        "ওকে", "ঠিক আছে", "ঠিকাছে", "আচ্ছা", "বুঝেছি", "হ্যাঁ", "না", "জি", "thik ache", "thikase", "thik ase", "accha", "achha", "bujhchi", "bujhechi", "hmm", "hm", "ji"
    };
    private static readonly HashSet<string> Thanks = new(StringComparer.Ordinal) {
        "thanks", "thank you", "thank you very much", "thanks a lot", "thank u", "thankyou",
        "ধন্যবাদ", "অনেক ধন্যবাদ", "ধন্যবাদ আপনাকে", "dhonnobad", "dhanyobad", "onek dhonnobad", "onek dhanyobad"
    };
    private static readonly HashSet<string> Wellbeing = new(StringComparer.Ordinal) {
        "how are you", "how are you doing", "কেমন আছো", "কেমন আছ", "কেমন আছেন", "তুমি কেমন আছো", "আপনি কেমন আছেন",
        "kemon acho", "kemon achen", "tumi kemon acho", "apni kemon achen"
    };
    public static ChatResponse? TryReply(string message)
    {
        var normalized = Regex.Replace(message.Normalize(NormalizationForm.FormC).ToLowerInvariant(), @"[\p{P}\p{S}]", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        string? intent = Greetings.Contains(normalized) ? "greeting" : Goodbyes.Contains(normalized) ? "goodbye" :
            Acknowledgements.Contains(normalized) ? "ack" : Thanks.Contains(normalized) ? "thanks" :
            Wellbeing.Contains(normalized) ? "wellbeing" : null;
        if (intent is null) return null;
        var language = LanguageService.Detect(message);
        if (language == "en" && Regex.IsMatch(normalized, @"\b(assalamualaikum|assalamu|alaikum|salam|salaam|biday|bidai|bhalo|valo|thik|thikase|accha|achha|bujhchi|bujhechi|dhonnobad|dhanyobad|kemon|acho|achen|onek|ji|hmm|hm)\b"))
            language = "banglish";
        var answer = (language, intent) switch {
            ("bn", "greeting") => "হ্যালো! On-Air CMS manual নিয়ে কী জানতে চান?",
            ("banglish", "greeting") => "Hello! On-Air CMS niye ki jante chan?",
            (_, "greeting") => "Hi! How can I help you with the On-Air CMS ?",
            ("bn", "goodbye") => "বিদায়! On-Air CMS নিয়ে সাহায্য লাগলে আবার আসবেন।",
            ("banglish", "goodbye") => "Biday! On-Air CMS niye shahajjo lagle abar ashben.",
            (_, "goodbye") => "Goodbye! Come back anytime you need help with the On-Air CMS manual.",
            ("bn", "thanks") => "আপনাকে স্বাগতম! On-Air CMS  নিয়ে আর কোনো প্রশ্ন থাকলে বলুন।",
            ("banglish", "thanks") => "Apnake shagotom! On-Air CMS niye ar kono proshno thakle bolun.",
            (_, "thanks") => "You're welcome! Let me know if you have another question about the CMS.",
            ("bn", "wellbeing") => "আমি On-Air CMS নিয়ে সাহায্য করতে প্রস্তুত। কী জানতে চান?",
            ("banglish", "wellbeing") => "Ami On-Air CMS niye shahajjo korte prostut. Ki jante chan?",
            (_, "wellbeing") => "I'm ready to help with the On-Air CMS. What would you like to know?",
            ("bn", _) => "ঠিক আছে। On-Air CMS নিয়ে সাহায্য লাগলে বলুন।",
            ("banglish", _) => "Thik ache. On-Air CMS niye shahajjo lagle bolun.",
            _ => "Okay. Let me know if you need help with the On-Air CMS manual."
        };
        return new(answer, language, false);
    }
}
