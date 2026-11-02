using QuizPrep.Api.Domain;

namespace QuizPrep.Api.Features.Questions;

/// <summary>Luật hợp lệ của câu hỏi, dùng chung cho form soạn câu hỏi và import.</summary>
public static class QuestionRules
{
    public const int MinOptions = 2;
    public const int MaxOptions = 10;
    public const int MaxTagLength = 50;

    public static IEnumerable<string> Validate(QuestionType type, string? content, IReadOnlyCollection<string> optionContents, int correctCount)
    {
        if (string.IsNullOrWhiteSpace(content))
            yield return "Nội dung câu hỏi là bắt buộc.";

        if (optionContents.Count is < MinOptions or > MaxOptions)
            yield return $"Câu hỏi phải có từ {MinOptions} đến {MaxOptions} đáp án.";

        if (optionContents.Any(string.IsNullOrWhiteSpace))
            yield return "Nội dung đáp án không được để trống.";

        if (correctCount == 0)
            yield return "Phải có ít nhất một đáp án đúng.";
        else if (type == QuestionType.SingleChoice && correctCount > 1)
            yield return "Câu một đáp án chỉ được có đúng một đáp án đúng.";
    }

    public static List<string> NormalizeTags(IEnumerable<string> tags) =>
        tags.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
