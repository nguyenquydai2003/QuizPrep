using System.ComponentModel.DataAnnotations;
using QuizPrep.Api.Domain;

namespace QuizPrep.Api.Features.Questions;

public record QuestionOptionDto(int Id, string Content, bool IsCorrect, int SortOrder);

public record QuestionListItemDto(
    int Id,
    int TopicId,
    string TopicName,
    string SubjectName,
    QuestionType Type,
    Difficulty Difficulty,
    string ContentPreview,
    IReadOnlyList<string> Tags,
    DateTime UpdatedAt);

public record QuestionDetailDto(
    int Id,
    int SubjectId,
    int TopicId,
    QuestionType Type,
    Difficulty Difficulty,
    string Content,
    string? Explanation,
    IReadOnlyList<string> Tags,
    IReadOnlyList<QuestionOptionDto> Options,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record QuestionQuery(
    int? SubjectId,
    int? TopicId,
    Difficulty? Difficulty,
    QuestionType? Type,
    string? Search,
    string? Tag,
    int Page = 1,
    int PageSize = 20);

public record SaveQuestionOptionRequest(string Content, bool IsCorrect);

public class SaveQuestionRequest : IValidatableObject
{
    public int TopicId { get; set; }
    public QuestionType Type { get; set; } = QuestionType.SingleChoice;
    public Difficulty Difficulty { get; set; } = Difficulty.Medium;

    [Required(ErrorMessage = "Nội dung câu hỏi là bắt buộc.")]
    public string Content { get; set; } = "";

    public string? Explanation { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<SaveQuestionOptionRequest> Options { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var errors = QuestionRules.Validate(Type, Content, Options.Select(o => o.Content).ToList(), Options.Count(o => o.IsCorrect));
        foreach (var error in errors)
            yield return new(error, [nameof(Options)]);

        if (Tags.Any(t => t.Trim().Length > QuestionRules.MaxTagLength))
            yield return new($"Mỗi tag tối đa {QuestionRules.MaxTagLength} ký tự.", [nameof(Tags)]);
    }

    public List<string> NormalizedTags() => QuestionRules.NormalizeTags(Tags);
}
