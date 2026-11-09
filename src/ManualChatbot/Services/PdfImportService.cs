using System.Text.Json;
using ManualChatbot.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ManualChatbot.Services;

public static class PdfImportService
{
    public static async Task ImportAsync(string pdfPath, string outputPath)
    {
        using var pdf = PdfDocument.Open(pdfPath);
        var pages = pdf.GetPages().Select(p => new ManualPage(p.Number, ContentOrderTextExtractor.GetText(p))).ToList();
        if (pages.All(p => string.IsNullOrWhiteSpace(p.Text)))
            throw new InvalidDataException("This PDF needs OCR. No searchable text could be extracted.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        var document = new ManualDocument("on-air-manual", "On-Air CMS User Manual", pages);
        var temporary = outputPath + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(document, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        File.Move(temporary, outputPath, true);
        Console.WriteLine($"Imported {pages.Count} pages. {pages.Count(p => p.Text.Length < 80)} pages may need OCR. Review before using. Embedding caches will be invalidated automatically.");
    }
}
