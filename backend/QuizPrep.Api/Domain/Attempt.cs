namespace QuizPrep.Api.Domain;

/// <summary>Một lượt làm bài (luyện tập, thi thử hoặc ôn câu sai).</summary>
public class Attempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public AttemptMode Mode { get; set; }
    public AttemptStatus Status { get; set; } = AttemptStatus.InProgress;
    public required string Title { get; set; }
    public int? ExamTemplateId { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Hạn nộp (chỉ thi thử). Đồng hồ tính theo server nên F5 không reset giờ.</summary>
    public DateTime? Deadline { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public int? PassScore { get; set; }

    public int TotalQuestions { get; set; }
    public int AnsweredCount { get; set; }
    public int CorrectCount { get; set; }

    /// <summary>% đúng (0-100), có khi đã nộp.</summary>
    public double? Score { get; set; }

    public List<AttemptQuestion> Questions { get; set; } = [];
}

/// <summary>
/// Câu hỏi trong bài làm, lưu bản chụp nội dung và đáp án (đã xáo) tại thời điểm tạo bài,
/// để admin sửa/xóa câu hỏi gốc không làm sai lịch sử.
/// </summary>
public class AttemptQuestion
{
    public int Id { get; set; }
    public Guid AttemptId { get; set; }
    public int Order { get; set; }

    /// <summary>Câu gốc, null nếu đã bị xóa.</summary>
    public int? QuestionId { get; set; }

    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = "";
    public int TopicId { get; set; }
    public string TopicName { get; set; } = "";
    public QuestionType Type { get; set; }
    public Difficulty Difficulty { get; set; }
    public string Content { get; set; } = "";
    public string? Explanation { get; set; }
    public List<AttemptOption> Options { get; set; } = [];

    public List<int> SelectedKeys { get; set; } = [];

    /// <summary>Kết quả chấm; null khi chưa chấm (thi thử chỉ chấm lúc nộp).</summary>
    public bool? IsCorrect { get; set; }

    public bool Flagged { get; set; }
    public DateTime? AnsweredAt { get; set; }
    public int TimeSpentSeconds { get; set; }
}

public class AttemptOption
{
    /// <summary>Id đáp án gốc, dùng làm khóa khi người làm bài chọn.</summary>
    public int Key { get; set; }

    public string Content { get; set; } = "";
    public bool IsCorrect { get; set; }
}

/// <summary>Thống kê từng câu của mỗi người, dùng cho ôn tập ngắt quãng (hộp Leitner) và dashboard.</summary>
public class UserQuestionStat
{
    public Guid UserId { get; set; }
    public int QuestionId { get; set; }
    public int AnsweredCount { get; set; }
    public int CorrectCount { get; set; }
    public int WrongCount { get; set; }
    public bool LastCorrect { get; set; }
    public DateTime LastAnsweredAt { get; set; }

    /// <summary>Hộp Leitner 1-5: sai về hộp 1, đúng lên một hộp; hộp càng cao càng lâu mới ôn lại.</summary>
    public int Box { get; set; } = 1;

    public DateTime NextReviewAt { get; set; }
}
