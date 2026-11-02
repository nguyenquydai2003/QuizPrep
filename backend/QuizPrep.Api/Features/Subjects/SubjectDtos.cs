using System.ComponentModel.DataAnnotations;

namespace QuizPrep.Api.Features.Subjects;

public record TopicDto(int Id, string Name, int SortOrder, int QuestionCount);

public record SubjectDto(int Id, string Name, string? Description, int SortOrder, IReadOnlyList<TopicDto> Topics)
{
    public int QuestionCount => Topics.Sum(t => t.QuestionCount);
}

public record SaveSubjectRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(1000)] string? Description,
    int SortOrder);

public record SaveTopicRequest(
    [Required, MaxLength(200)] string Name,
    int SortOrder);
