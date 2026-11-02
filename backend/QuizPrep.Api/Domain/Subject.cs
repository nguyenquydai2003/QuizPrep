namespace QuizPrep.Api.Domain;

/// <summary>Môn học, ví dụ "Lập trình C#", "SQL".</summary>
public class Subject
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }

    public List<Topic> Topics { get; set; } = [];
}

/// <summary>Chủ đề thuộc một môn, ví dụ "OOP", "LINQ".</summary>
public class Topic
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public required string Name { get; set; }
    public int SortOrder { get; set; }

    public List<Question> Questions { get; set; } = [];
}
