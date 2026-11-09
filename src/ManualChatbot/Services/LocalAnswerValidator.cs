using System.Text.RegularExpressions;
using ManualChatbot.Models;

namespace ManualChatbot.Services;

// Conservative checks. These are not a semantic proof of factual correctness.
public static class LocalAnswerValidator
{
    private static readonly Regex UiLabel = new("(?<!\\w)[`\"“]([^`\"”\\r\\n]{2,70})[`\"”]", RegexOptions.Compiled);
    public static bool IsSafe(string answer, IReadOnlyList<SearchHit> hits, string[]? evidenceIds)
    {
        if (string.IsNullOrWhiteSpace(answer) || answer.Length > 7000 || evidenceIds is null || evidenceIds.Length == 0) return false;
        var cited = hits.Where(h => evidenceIds.Contains(h.Chunk.Id)).Select(h => h.Chunk.Text).ToArray();
        if (cited.Length == 0) return false;
        var source = string.Join(" ", cited);
        // Reject fabricated, quoted UI labels; retain prose in Bangla/Banglish.
        foreach (Match match in UiLabel.Matches(answer))
        {
            var label = match.Groups[1].Value.Trim();
            if (label.Length > 35 || label.Contains(' ' ) && label.Split(' ').Length > 5) continue;
            if (Regex.IsMatch(label, @"[A-Za-z]") && !source.Contains(label, StringComparison.OrdinalIgnoreCase)) return false;
        }
        // Disallow a common documented error for this specific workflow.
        if (source.Contains("INSERT IMAGE FROM RICH", StringComparison.OrdinalIgnoreCase) &&
            Regex.IsMatch(answer, @"\+\s*Insert\s*(?:→|>|then)\s*Image", RegexOptions.IgnoreCase) &&
            !source.Contains("+ Insert", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}
