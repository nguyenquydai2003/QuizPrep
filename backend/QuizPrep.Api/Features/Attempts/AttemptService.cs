using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QuizPrep.Api.Common;
using QuizPrep.Api.Data;
using QuizPrep.Api.Domain;

namespace QuizPrep.Api.Features.Attempts;

public partial class AttemptService(AppDbContext db, QuestionPicker picker, TimeProvider clock)
{
    public const int MaxQuestionsPerSession = 200;

    /// <summary>Cho phép nộp trễ một chút do độ trễ mạng.</summary>
    private static readonly TimeSpan SubmitGrace = TimeSpan.FromSeconds(15);

    /// <summary>Khoảng cách ôn lại theo hộp Leitner (index = hộp 1..5).</summary>
    private static readonly TimeSpan[] ReviewIntervals =
        [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromDays(1), TimeSpan.FromDays(3), TimeSpan.FromDays(7), TimeSpan.FromDays(14)];

    private const int MaxBox = 5;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<Attempt> CreatePracticeAsync(Guid userId, PracticeRequest request, CancellationToken ct)
    {
        var query = picker.Filter(request.SubjectId, request.TopicId, request.Difficulty);
        if (request.OnlyUnseen)
            query = query.Where(q => !db.UserQuestionStats.Any(s => s.UserId == userId && s.QuestionId == q.Id));

        var questions = await picker.PickRandomAsync(query, request.Count, [], ct);
        if (questions.Count == 0)
            throw new AppProblemException(request.OnlyUnseen
                ? "Bạn đã làm hết các câu trong phạm vi này. Bỏ chọn \"Chỉ câu chưa làm\" để luyện lại."
                : "Không có câu hỏi nào phù hợp với lựa chọn.");

        var title = "Luyện tập: " + await DescribeScopeAsync(request.SubjectId, request.TopicId, ct);
        return await SaveNewAsync(userId, AttemptMode.Practice, title, questions, shuffleOptions: true, ct);
    }

    public async Task<Attempt> CreateExamAsync(Guid userId, int templateId, bool isAdmin, CancellationToken ct)
    {
        var template = await db.ExamTemplates.AsNoTracking().Include(t => t.Rules)
            .FirstOrDefaultAsync(t => t.Id == templateId && (t.IsPublished || isAdmin), ct)
            ?? throw AppProblemException.NotFound("Đề thi không tồn tại hoặc chưa được phát hành.");

        var picked = new List<Question>();
        var pickedIds = new HashSet<int>();
        foreach (var rule in template.Rules.OrderBy(r => r.SortOrder))
        {
            var questions = await picker.PickRandomAsync(picker.Filter(rule.SubjectId, rule.TopicId, rule.Difficulty), rule.QuestionCount, pickedIds, ct);
            if (questions.Count < rule.QuestionCount)
                throw new AppProblemException(
                    $"Ngân hàng không đủ câu cho \"{await DescribeScopeAsync(rule.SubjectId, rule.TopicId, ct)}\": cần {rule.QuestionCount}, chỉ có {questions.Count}.");
            picked.AddRange(questions);
            pickedIds.UnionWith(questions.Select(q => q.Id));
        }

        // Trộn thứ tự để các câu cùng chủ đề không nằm liền nhau
        var shuffled = picked.ToArray();
        Random.Shared.Shuffle(shuffled);

        var attempt = await SaveNewAsync(userId, AttemptMode.Exam, template.Title, shuffled, template.ShuffleOptions, ct, a =>
        {
            a.ExamTemplateId = template.Id;
            a.PassScore = template.PassScore;
            a.Deadline = a.StartedAt.AddMinutes(template.DurationMinutes);
        });
        return attempt;
    }

    public async Task<Attempt> CreateReviewAsync(Guid userId, ReviewRequest request, CancellationToken ct)
    {
        var now = Now;
        var dueIds = await DueQuery(userId, now)
            .Where(s => request.SubjectId == null || db.Questions.Any(q => q.Id == s.QuestionId && q.Topic.SubjectId == request.SubjectId))
            .OrderBy(s => s.NextReviewAt).ThenByDescending(s => s.WrongCount)
            .Select(s => s.QuestionId)
            .Take(request.Count)
            .ToListAsync(ct);
        if (dueIds.Count == 0)
            throw new AppProblemException("Hiện không có câu nào cần ôn. Làm thêm bài luyện tập nhé!");

        var questions = await picker.LoadAsync(dueIds, ct);
        return await SaveNewAsync(userId, AttemptMode.Review, "Ôn câu sai", questions, shuffleOptions: true, ct);
    }

    public async Task<ReviewSummaryDto> GetReviewSummaryAsync(Guid userId, CancellationToken ct) => new(
        await DueQuery(userId, Now).CountAsync(ct),
        await db.UserQuestionStats.CountAsync(s => s.UserId == userId && s.WrongCount > 0, ct));

    public IQueryable<UserQuestionStat> DueQuery(Guid userId, DateTime now) =>
        db.UserQuestionStats.Where(s => s.UserId == userId && s.WrongCount > 0 && s.NextReviewAt <= now);

    /// <summary>Tải bài làm của người dùng; nếu thi thử đã quá giờ thì tự nộp.</summary>
    public async Task<Attempt> GetAsync(Guid userId, Guid attemptId, CancellationToken ct)
    {
        var attempt = await db.Attempts
            .Include(a => a.Questions.OrderBy(q => q.Order))
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct)
            ?? throw AppProblemException.NotFound("Không tìm thấy bài làm.");

        if (IsOverdue(attempt)) await SubmitAsync(attempt, ct);
        return attempt;
    }

    public async Task<AttemptQuestion> AnswerAsync(Attempt attempt, int attemptQuestionId, AnswerRequest request, CancellationToken ct)
    {
        EnsureInProgress(attempt);
        if (IsOverdue(attempt))
        {
            await SubmitAsync(attempt, ct);
            throw AppProblemException.Conflict("Đã hết giờ, bài đã được nộp tự động.");
        }

        var question = attempt.Questions.FirstOrDefault(q => q.Id == attemptQuestionId)
            ?? throw AppProblemException.NotFound("Câu hỏi không thuộc bài làm này.");

        var keys = request.SelectedKeys.Distinct().ToList();
        if (keys.Any(k => question.Options.All(o => o.Key != k)))
            throw new AppProblemException("Đáp án không hợp lệ.");
        if (question.Type == QuestionType.SingleChoice && keys.Count > 1)
            throw new AppProblemException("Câu này chỉ được chọn một đáp án.");

        var gradedImmediately = attempt.Mode != AttemptMode.Exam;
        if (gradedImmediately)
        {
            if (question.IsCorrect != null) throw AppProblemException.Conflict("Câu này đã được chấm.");
            if (keys.Count == 0) throw new AppProblemException("Hãy chọn đáp án.");
        }

        var now = Now;
        question.SelectedKeys = keys;
        question.AnsweredAt = keys.Count > 0 ? now : null;
        question.TimeSpentSeconds += Math.Clamp(request.TimeSpentSeconds, 0, 3600);
        if (gradedImmediately)
        {
            question.IsCorrect = Grade(question);
            await RecordStatAsync(attempt.UserId, question, now, ct);
        }
        RefreshCounts(attempt);
        await db.SaveChangesAsync(ct);
        return question;
    }

    public async Task<AttemptQuestion> FlagAsync(Attempt attempt, int attemptQuestionId, bool flagged, CancellationToken ct)
    {
        EnsureInProgress(attempt);
        var question = attempt.Questions.FirstOrDefault(q => q.Id == attemptQuestionId)
            ?? throw AppProblemException.NotFound("Câu hỏi không thuộc bài làm này.");
        question.Flagged = flagged;
        await db.SaveChangesAsync(ct);
        return question;
    }

    public async Task SubmitAsync(Attempt attempt, CancellationToken ct)
    {
        if (attempt.Status == AttemptStatus.Submitted) return;

        var now = Now;
        if (attempt.Mode == AttemptMode.Exam)
        {
            // Thi thử: chấm toàn bộ lúc nộp, câu bỏ trống tính sai (nhưng không đưa vào thống kê ôn tập)
            foreach (var q in attempt.Questions)
            {
                q.IsCorrect = q.SelectedKeys.Count > 0 && Grade(q);
                if (q.SelectedKeys.Count > 0) await RecordStatAsync(attempt.UserId, q, now, ct);
            }
        }

        attempt.Status = AttemptStatus.Submitted;
        attempt.SubmittedAt = attempt.Deadline is { } deadline && now > deadline ? deadline : now;
        RefreshCounts(attempt);

        // Thi thử tính trên tổng số câu; luyện tập/ôn tập chỉ tính trên số câu đã làm
        var denominator = attempt.Mode == AttemptMode.Exam ? attempt.TotalQuestions : attempt.AnsweredCount;
        attempt.Score = denominator == 0 ? null : Math.Round(100.0 * attempt.CorrectCount / denominator, 1);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Attempt> SaveNewAsync(Guid userId, AttemptMode mode, string title, IReadOnlyList<Question> questions,
        bool shuffleOptions, CancellationToken ct, Action<Attempt>? configure = null)
    {
        var attempt = new Attempt
        {
            UserId = userId,
            Mode = mode,
            Title = title,
            StartedAt = Now,
            TotalQuestions = questions.Count,
            Questions = questions.Select((q, i) => Snapshot(q, i + 1, shuffleOptions)).ToList(),
        };
        configure?.Invoke(attempt);
        db.Attempts.Add(attempt);
        await db.SaveChangesAsync(ct);
        return attempt;
    }

    private static AttemptQuestion Snapshot(Question q, int order, bool shuffleOptions)
    {
        var options = q.Options.OrderBy(o => o.SortOrder)
            .Select(o => new AttemptOption { Key = o.Id, Content = o.Content, IsCorrect = o.IsCorrect })
            .ToArray();
        // Không xáo khi đáp án tham chiếu đến nhau ("Cả A và B", "Tất cả đều đúng"...)
        if (shuffleOptions && !options.Any(o => ReferencesOtherOptions().IsMatch(o.Content)))
            Random.Shared.Shuffle(options);

        return new AttemptQuestion
        {
            Order = order,
            QuestionId = q.Id,
            SubjectId = q.Topic.SubjectId,
            SubjectName = q.Topic.Subject.Name,
            TopicId = q.TopicId,
            TopicName = q.Topic.Name,
            Type = q.Type,
            Difficulty = q.Difficulty,
            Content = q.Content,
            Explanation = q.Explanation,
            Options = options.ToList(),
        };
    }

    private static bool Grade(AttemptQuestion q) =>
        q.SelectedKeys.ToHashSet().SetEquals(q.Options.Where(o => o.IsCorrect).Select(o => o.Key));

    private static void RefreshCounts(Attempt attempt)
    {
        attempt.AnsweredCount = attempt.Questions.Count(q => q.SelectedKeys.Count > 0);
        attempt.CorrectCount = attempt.Questions.Count(q => q.IsCorrect == true);
    }

    /// <summary>Cập nhật thống kê câu hỏi của người dùng và lịch ôn theo hộp Leitner.</summary>
    private async Task RecordStatAsync(Guid userId, AttemptQuestion q, DateTime now, CancellationToken ct)
    {
        if (q.QuestionId is not { } questionId || q.IsCorrect is not { } correct) return;
        // Câu gốc đã bị xóa thì không còn gì để ôn
        if (!await db.Questions.AnyAsync(x => x.Id == questionId, ct)) return;

        var stat = await db.UserQuestionStats.FindAsync([userId, questionId], ct);
        if (stat == null)
        {
            stat = new UserQuestionStat { UserId = userId, QuestionId = questionId, Box = 1 };
            db.UserQuestionStats.Add(stat);
        }

        stat.AnsweredCount++;
        if (correct)
        {
            stat.CorrectCount++;
            stat.Box = Math.Min(stat.Box + 1, MaxBox);
        }
        else
        {
            stat.WrongCount++;
            stat.Box = 1;
        }
        stat.LastCorrect = correct;
        stat.LastAnsweredAt = now;
        stat.NextReviewAt = now + ReviewIntervals[stat.Box];
    }

    private bool IsOverdue(Attempt a) =>
        a.Status == AttemptStatus.InProgress && a.Deadline is { } deadline && Now > deadline + SubmitGrace;

    private static void EnsureInProgress(Attempt attempt)
    {
        if (attempt.Status == AttemptStatus.InProgress) return;
        var timedOut = attempt.Deadline is { } deadline && attempt.SubmittedAt >= deadline;
        throw AppProblemException.Conflict(timedOut ? "Đã hết giờ, bài đã được nộp tự động." : "Bài làm đã được nộp.");
    }

    private async Task<string> DescribeScopeAsync(int? subjectId, int? topicId, CancellationToken ct)
    {
        if (topicId != null)
            return await db.Topics.Where(t => t.Id == topicId).Select(t => t.Subject.Name + " / " + t.Name).FirstOrDefaultAsync(ct) ?? "Chủ đề đã xóa";
        if (subjectId != null)
            return await db.Subjects.Where(s => s.Id == subjectId).Select(s => s.Name).FirstOrDefaultAsync(ct) ?? "Môn đã xóa";
        return "Tất cả";
    }

    [GeneratedRegex(@"\b(t[ấa]t c[ảa]|c[ảa] hai|c[ảa] [A-J]\b|kh[ôo]ng c[óo] (đáp án|phương án|câu)|[A-J]\s*(và|,|&)\s*[A-J]\b|all of|none of|both)", RegexOptions.IgnoreCase)]
    private static partial Regex ReferencesOtherOptions();
}
