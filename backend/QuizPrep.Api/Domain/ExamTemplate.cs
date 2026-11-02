namespace QuizPrep.Api.Domain;

/// <summary>Cấu hình đề thi thử: mỗi lần thi sinh đề ngẫu nhiên theo ma trận Rules.</summary>
public class ExamTemplate
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public int DurationMinutes { get; set; } = 60;

    /// <summary>Điểm đạt, tính theo % số câu đúng (0-100).</summary>
    public int PassScore { get; set; } = 70;

    public bool ShuffleOptions { get; set; } = true;
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ExamTemplateRule> Rules { get; set; } = [];
}

/// <summary>
/// Một dòng ma trận: lấy QuestionCount câu từ chủ đề (hoặc cả môn), có thể lọc độ khó.
/// SubjectId/TopicId không ràng buộc khóa ngoại để xóa chủ đề không làm hỏng đề; lúc sinh đề sẽ báo lỗi nếu thiếu.
/// </summary>
public class ExamTemplateRule
{
    public int Id { get; set; }
    public int ExamTemplateId { get; set; }
    public int? SubjectId { get; set; }
    public int? TopicId { get; set; }
    public Difficulty? Difficulty { get; set; }
    public int QuestionCount { get; set; }
    public int SortOrder { get; set; }
}
