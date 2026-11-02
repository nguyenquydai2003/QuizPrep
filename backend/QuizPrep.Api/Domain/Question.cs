namespace QuizPrep.Api.Domain;

public class Question
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public Topic Topic { get; set; } = null!;
    public QuestionType Type { get; set; } = QuestionType.SingleChoice;
    public Difficulty Difficulty { get; set; } = Difficulty.Medium;

    /// <summary>Nội dung Markdown, có thể chứa code block.</summary>
    public required string Content { get; set; }

    /// <summary>Lời giải thích (Markdown).</summary>
    public string? Explanation { get; set; }

    public List<string> Tags { get; set; } = [];

    /// <summary>SHA-256 của nội dung + đáp án đã chuẩn hóa, dùng để phát hiện câu trùng.</summary>
    public string ContentHash { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<QuestionOption> Options { get; set; } = [];
}

public class QuestionOption
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public required string Content { get; set; }
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }
}
