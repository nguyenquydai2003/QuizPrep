using QuizPrep.Api.Domain;
using QuizPrep.Api.Features.Questions;

namespace QuizPrep.Api.Features.Import;

/// <summary>Lỗi định dạng file, thông báo hiển thị thẳng cho người dùng.</summary>
public class ImportFormatException(string message) : Exception(message);

/// <summary>Kết quả đọc thô từ file, chưa chuẩn hóa.</summary>
public class ParsedQuestion
{
    public required string Source { get; init; }
    public string? SubjectName { get; set; }
    public string? TopicName { get; set; }
    public string? DifficultyText { get; set; }
    public string? TypeText { get; set; }
    public string? AnswerText { get; set; }
    public string Content { get; set; } = "";
    public string? Explanation { get; set; }
    public List<ParsedOption> Options { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public class ParsedOption
{
    public required char Letter { get; init; }
    public string Content { get; set; } = "";

    /// <summary>Có dấu * trước/sau chữ cái.</summary>
    public bool Marked { get; init; }

    /// <summary>Toàn bộ đáp án được in đậm / gạch chân / tô màu (chỉ có ở Word).</summary>
    public bool Emphasized { get; init; }
}

/// <summary>Một câu hỏi trong màn xem trước. Client gửi lại khi xác nhận lưu, server phân tích lại.</summary>
public class ImportItemDto
{
    public int Index { get; set; }
    public string Source { get; set; } = "";
    public string? SubjectName { get; set; }
    public string? TopicName { get; set; }
    public QuestionType Type { get; set; } = QuestionType.SingleChoice;
    public Difficulty Difficulty { get; set; } = Difficulty.Medium;
    public string Content { get; set; } = "";
    public string? Explanation { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<SaveQuestionOptionRequest> Options { get; set; } = [];

    // Kết quả phân tích
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public int? TopicId { get; set; }
    public bool CreatesSubject { get; set; }
    public bool CreatesTopic { get; set; }
    public int? DuplicateOfQuestionId { get; set; }
    public int? DuplicateOfIndex { get; set; }
}

public record ImportPreviewResponse(string FileName, IReadOnlyList<ImportItemDto> Items);

public class ImportCommitRequest
{
    public int? DefaultTopicId { get; set; }
    public List<ImportItemDto> Items { get; set; } = [];
}

public record ImportCommitResponse(int Created, int Skipped, int SubjectsCreated, int TopicsCreated, IReadOnlyList<string> SkippedReasons);
