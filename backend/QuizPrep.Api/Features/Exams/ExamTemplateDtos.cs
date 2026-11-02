using System.ComponentModel.DataAnnotations;
using QuizPrep.Api.Domain;

namespace QuizPrep.Api.Features.Exams;

public record ExamRuleDto(
    int Id,
    int? SubjectId,
    string? SubjectName,
    int? TopicId,
    string? TopicName,
    Difficulty? Difficulty,
    int QuestionCount,
    int AvailableCount);

public record ExamTemplateDto(
    int Id,
    string Title,
    string? Description,
    int DurationMinutes,
    int PassScore,
    bool ShuffleOptions,
    bool IsPublished,
    int TotalQuestions,
    bool HasEnoughQuestions,
    IReadOnlyList<ExamRuleDto> Rules,
    DateTime UpdatedAt);

/// <summary>Đề thi hiển thị cho người làm bài, kèm kết quả của chính họ.</summary>
public record AvailableExamDto(
    int Id,
    string Title,
    string? Description,
    int DurationMinutes,
    int PassScore,
    int TotalQuestions,
    int AttemptCount,
    double? BestScore,
    double? LastScore);

public record SaveExamRuleRequest(int? SubjectId, int? TopicId, Difficulty? Difficulty, int QuestionCount);

public class SaveExamTemplateRequest : IValidatableObject
{
    public const int MaxRules = 50;
    public const int MaxTotalQuestions = 500;

    [Required(ErrorMessage = "Nhập tên đề thi."), MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Range(1, 600, ErrorMessage = "Thời gian làm bài từ 1 đến 600 phút.")]
    public int DurationMinutes { get; set; } = 60;

    [Range(0, 100, ErrorMessage = "Điểm đạt từ 0 đến 100%.")]
    public int PassScore { get; set; } = 70;

    public bool ShuffleOptions { get; set; } = true;
    public bool IsPublished { get; set; }
    public List<SaveExamRuleRequest> Rules { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Title))
            yield return new("Nhập tên đề thi.", [nameof(Title)]);
        if (Rules.Count is 0 or > MaxRules)
            yield return new($"Ma trận đề cần từ 1 đến {MaxRules} dòng.", [nameof(Rules)]);
        if (Rules.Any(r => r.SubjectId == null && r.TopicId == null))
            yield return new("Mỗi dòng ma trận phải chọn môn hoặc chủ đề.", [nameof(Rules)]);
        if (Rules.Any(r => r.QuestionCount < 1))
            yield return new("Số câu mỗi dòng phải lớn hơn 0.", [nameof(Rules)]);
        if (Rules.Sum(r => r.QuestionCount) > MaxTotalQuestions)
            yield return new($"Tổng số câu tối đa {MaxTotalQuestions}.", [nameof(Rules)]);
    }
}
