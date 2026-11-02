using System.ComponentModel.DataAnnotations;
using QuizPrep.Api.Domain;

namespace QuizPrep.Api.Features.Attempts;

public record PracticeRequest(
    int? SubjectId,
    int? TopicId,
    Difficulty? Difficulty,
    [Range(1, AttemptService.MaxQuestionsPerSession)] int Count = 20,
    bool OnlyUnseen = false);

public record StartExamRequest(int ExamTemplateId);

public record ReviewRequest(int? SubjectId, [Range(1, AttemptService.MaxQuestionsPerSession)] int Count = 20);

public record AnswerRequest(List<int> SelectedKeys, int TimeSpentSeconds = 0);

public record FlagRequest(bool Flagged);

public record AttemptOptionDto(int Key, string Content, bool? IsCorrect);

public record AttemptQuestionDto(
    int Id,
    int Order,
    QuestionType Type,
    Difficulty Difficulty,
    string SubjectName,
    string TopicName,
    string Content,
    IReadOnlyList<AttemptOptionDto> Options,
    IReadOnlyList<int> SelectedKeys,
    bool Flagged,
    bool Revealed,
    bool? IsCorrect,
    string? Explanation,
    int TimeSpentSeconds);

public record AttemptDto(
    Guid Id,
    AttemptMode Mode,
    AttemptStatus Status,
    string Title,
    int? ExamTemplateId,
    DateTime StartedAt,
    DateTime? Deadline,
    DateTime? SubmittedAt,
    DateTime ServerNow,
    int? PassScore,
    int TotalQuestions,
    int AnsweredCount,
    int CorrectCount,
    double? Score,
    bool? Passed,
    IReadOnlyList<AttemptQuestionDto> Questions);

public record AttemptSummaryDto(
    Guid Id,
    AttemptMode Mode,
    AttemptStatus Status,
    string Title,
    DateTime StartedAt,
    DateTime? Deadline,
    DateTime? SubmittedAt,
    int TotalQuestions,
    int AnsweredCount,
    int CorrectCount,
    double? Score,
    int? PassScore,
    bool? Passed);

public record ReviewSummaryDto(int DueCount, int WrongQuestionCount);

public static class AttemptMapper
{
    /// <summary>
    /// Thi thử chỉ lộ đáp án sau khi nộp; luyện tập/ôn tập lộ ngay sau khi trả lời từng câu.
    /// </summary>
    public static bool IsRevealed(Attempt a, AttemptQuestion q) =>
        a.Status == AttemptStatus.Submitted || (a.Mode != AttemptMode.Exam && q.IsCorrect != null);

    public static AttemptQuestionDto ToDto(Attempt a, AttemptQuestion q)
    {
        var revealed = IsRevealed(a, q);
        return new AttemptQuestionDto(
            q.Id, q.Order, q.Type, q.Difficulty, q.SubjectName, q.TopicName, q.Content,
            q.Options.Select(o => new AttemptOptionDto(o.Key, o.Content, revealed ? o.IsCorrect : null)).ToList(),
            q.SelectedKeys, q.Flagged, revealed,
            revealed ? q.IsCorrect : null,
            revealed ? q.Explanation : null,
            q.TimeSpentSeconds);
    }

    public static AttemptDto ToDto(Attempt a) => new(
        a.Id, a.Mode, a.Status, a.Title, a.ExamTemplateId, a.StartedAt, a.Deadline, a.SubmittedAt, DateTime.UtcNow,
        a.PassScore, a.TotalQuestions, a.AnsweredCount, a.CorrectCount, a.Score, Passed(a),
        a.Questions.OrderBy(q => q.Order).Select(q => ToDto(a, q)).ToList());

    public static bool? Passed(Attempt a) =>
        a.Score is { } score && a.PassScore is { } pass ? score >= pass : null;
}
