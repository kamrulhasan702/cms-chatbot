using System.Text.RegularExpressions;
using ManualChatbot.Models;

namespace ManualChatbot.Services;

// Stable wording for the image-insertion procedure, backed by the manual itself.
// If the manual is replaced and no longer contains these steps, this shortcut is disabled.
public static class ManualProcedureService
{
    public static bool IsRichTextImageQuestion(string question)
    {
        var q = question.ToLowerInvariant();
        var image = Regex.IsMatch(q, @"\b(image|picture|photo|chobi|chhobi|ছবি|ইমেজ)\b", RegexOptions.IgnoreCase)
            || q.Contains("ছবি") || q.Contains("ইমেজ");
        var editor = q.Contains("rich-text") || q.Contains("rich text") || q.Contains("রিচ টেক্সট") || q.Contains("রিচ-টেক্সট");
        var insert = Regex.IsMatch(q, @"\b(insert|add|upload|korbo|kivabe|kibhabe|how)\b", RegexOptions.IgnoreCase)
            || q.Contains("ইনসার্ট") || q.Contains("যোগ") || q.Contains("কীভাবে");
        return image && editor && insert && !q.Contains("screenshot");
    }

    public static ChatResponse? TryAnswer(string question, string language, SearchService search, FigureService? figures)
    {
        if (!IsRichTextImageQuestion(question)) return null;
        var hits = search.FindHeading("INSERT IMAGE FROM RICH-TEXT EDITOR");
        // Check the complete documented workflow before using a fixed translation.
        var manual = string.Join(" ", hits.Select(h => h.Chunk.Text));
        if (!manual.Contains("Click the Image button", StringComparison.OrdinalIgnoreCase)
            || !manual.Contains("Choose File", StringComparison.OrdinalIgnoreCase)
            || !manual.Contains("Add Image Caption", StringComparison.OrdinalIgnoreCase)
            || !manual.Contains("Insert Image", StringComparison.OrdinalIgnoreCase)) return null;

        var answer = language switch
        {
            "bn" => "১. Rich-text editor-এর toolbar-এ Image button-এ ক্লিক করুন।\n২. Choose an Image উইন্ডোতে Search tab থেকে media library-র ছবি নির্বাচন করুন, অথবা Upload tab খুলুন।\n৩. নতুন ছবি যোগ করতে Choose File-এ ক্লিক করে ছবি বেছে নিন (AVIF, GIF, JPG, JPEG, PNG বা WEBP; সর্বোচ্চ 1.0 MB)। Title দেওয়া বাধ্যতামূলক; Caption, Tags ও Attribute ঐচ্ছিক। তারপর Upload-এ ক্লিক করুন।\n৪. Add Image Caption উইন্ডোতে Caption লিখুন বা পরিবর্তন করুন এবং Alignment (Full width, Left বা Right) নির্বাচন করুন।\n৫. Insert Image-এ ক্লিক করলে ছবিটি article-এ যুক্ত হবে।",
            "banglish" => "1. Rich-text editor-er toolbar-e Image button-e click korun.\n2. Choose an Image window-te Search tab theke media library-r chobi select korun, othoba Upload tab khulun.\n3. Notun chobi dite Choose File click kore file select korun (AVIF, GIF, JPG, JPEG, PNG ba WEBP; maximum 1.0 MB). Title dewa required; Caption, Tags o Attribute optional. Tarpor Upload click korun.\n4. Add Image Caption window-te Caption likhun ba edit korun, ar Alignment (Full width, Left ba Right) select korun.\n5. Insert Image click korle chobiti article-e add hobe.",
            _ => "1. Click Image in the rich-text editor toolbar.\n2. In Choose an Image, use the Search tab to select an image from the media library, or open the Upload tab.\n3. To upload, click Choose File and select an image (AVIF, GIF, JPG, JPEG, PNG or WEBP; maximum 1.0 MB). Title is required; Caption, Tags and Attribute are optional. Click Upload.\n4. In Add Image Caption, enter or edit the Caption and select Alignment (Full width, Left or Right).\n5. Click Insert Image to add the image to the article."
        };
        return new ChatResponse(answer, language, false,
            figures?.Select(hits, hits.Select(h => h.Chunk.Id).ToArray(), question, answer) ?? []);
    }
}
