using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizPrep.Api.Common;
using QuizPrep.Api.Data;
using QuizPrep.Api.Domain;
using QuizPrep.Api.Features.Attempts;

namespace QuizPrep.Api.Features.Stats;

public record ScorePointDto(Guid AttemptId, string Title, DateTime SubmittedAt, double Score, int? PassScore);

public record TopicStatDto(int TopicId, string SubjectName, string TopicName, int Answered, int Correct, double Accuracy);

public record ActivityDayDto(string Date, int Count);

public record WrongQuestionDto(int QuestionId, string ContentPreview, string TopicName, int WrongCount, int AnsweredCount);

public record StatsOverviewDto(
    string DisplayName,
    int QuestionBankSize,
    int DistinctAnswered,
    double CoveragePercent,
    int TotalAnswered,
    int TotalCorrect,
    double AccuracyPercent,
    double AvgSecondsPerQuestion,
    int ExamsTaken,
    int ExamsPassed,
    double? BestExamScore,
    double? AvgExamScore,
    int CurrentStreak,
    int LongestStreak,
    int DueReviewCount,
    IReadOnlyList<ScorePointDto> ScoreTrend,
    IReadOnlyList<TopicStatDto> Topics,
    IReadOnlyList<ActivityDayDto> Activity,
    IReadOnlyList<WrongQuestionDto> TopWrong,
    IReadOnlyList<AttemptSummaryDto> InProgress,
    IReadOnlyList<AttemptSummaryDto> Recent);

[ApiController]
[Route("api/stats")]
[Authorize]
public class StatsController(AppDbContext db, AttemptService attempts) : ControllerBase
{
    private const int ActivityDays = 365;
    private const int TrendSize = 30;

    /// <param name="userId">Chỉ admin được xem của người khác.</param>
    /// <param name="tzOffsetMinutes">Lệch múi giờ của trình duyệt so với UTC (Việt Nam = 420), để tính ngày học cho đúng.</param>
    [HttpGet("overview")]
    public async Task<ActionResult<StatsOverviewDto>> Overview([FromQuery] Guid? userId, [FromQuery] int tzOffsetMinutes = 0, CancellationToken ct = default)
    {
        var targetId = userId ?? User.GetUserId();
        if (targetId != User.GetUserId() && !User.IsInRole(Roles.Admin)) return Forbid();

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == targetId, ct);
        if (user is null) return NotFound();

        var offset = TimeSpan.FromMinutes(Math.Clamp(tzOffsetMinutes, -14 * 60, 14 * 60));
        var now = DateTime.UtcNow;

        // Mọi câu đã trả lời và đã chấm (thi thử đang làm dở chưa được tính)
        var answers = await (
            from q in db.AttemptQuestions
            join a in db.Attempts on q.AttemptId equals a.Id
            where a.UserId == targetId && q.IsCorrect != null && q.AnsweredAt != null
            select new { q.AnsweredAt, IsCorrect = q.IsCorrect!.Value, q.TopicId, q.TopicName, q.SubjectName, q.TimeSpentSeconds, a.StartedAt })
            .ToListAsync(ct);

        var totalAnswered = answers.Count;
        var totalCorrect = answers.Count(x => x.IsCorrect);

        var bankSize = await db.Questions.CountAsync(ct);
        var distinctAnswered = await db.UserQuestionStats.CountAsync(s => s.UserId == targetId, ct);

        // Ngày học theo giờ địa phương
        var activeDays = answers
            .GroupBy(x => DateOnly.FromDateTime(x.AnsweredAt!.Value + offset))
            .ToDictionary(g => g.Key, g => g.Count());
        var today = DateOnly.FromDateTime(now + offset);
        var activity = activeDays.Where(d => d.Key > today.AddDays(-ActivityDays))
            .OrderBy(d => d.Key)
            .Select(d => new ActivityDayDto(d.Key.ToString("yyyy-MM-dd"), d.Value))
            .ToList();
        var (currentStreak, longestStreak) = Streaks(activeDays.Keys, today);

        var topics = answers
            .GroupBy(x => x.TopicId)
            .Select(g =>
            {
                var latest = g.MaxBy(x => x.AnsweredAt)!;
                var correct = g.Count(x => x.IsCorrect);
                return new TopicStatDto(g.Key, latest.SubjectName, latest.TopicName, g.Count(), correct, Math.Round(100.0 * correct / g.Count(), 1));
            })
            .OrderBy(t => t.SubjectName).ThenBy(t => t.TopicName)
            .ToList();

        var exams = await db.Attempts.AsNoTracking()
            .Where(a => a.UserId == targetId && a.Mode == AttemptMode.Exam && a.Status == AttemptStatus.Submitted && a.Score != null)
            .OrderByDescending(a => a.SubmittedAt)
            .Select(a => new { a.Id, a.Title, a.SubmittedAt, Score = a.Score!.Value, a.PassScore })
            .ToListAsync(ct);
        var trend = exams.Take(TrendSize).Reverse()
            .Select(e => new ScorePointDto(e.Id, e.Title, e.SubmittedAt!.Value, e.Score, e.PassScore))
            .ToList();

        var topWrong = await (
            from s in db.UserQuestionStats
            join q in db.Questions on s.QuestionId equals q.Id
            where s.UserId == targetId && s.WrongCount > 0
            orderby s.WrongCount descending, s.LastAnsweredAt descending
            select new WrongQuestionDto(q.Id, q.Content.Substring(0, 200), q.Topic.Name, s.WrongCount, s.AnsweredCount))
            .Take(10)
            .ToListAsync(ct);

        var inProgress = await db.Attempts.AsNoTracking()
            .Where(a => a.UserId == targetId && a.Status == AttemptStatus.InProgress)
            .OrderByDescending(a => a.StartedAt).Take(5).ToListAsync(ct);
        var recent = await db.Attempts.AsNoTracking()
            .Where(a => a.UserId == targetId && a.Status == AttemptStatus.Submitted)
            .OrderByDescending(a => a.SubmittedAt).Take(5).ToListAsync(ct);

        return new StatsOverviewDto(
            user.DisplayName,
            bankSize,
            distinctAnswered,
            bankSize == 0 ? 0 : Math.Round(100.0 * distinctAnswered / bankSize, 1),
            totalAnswered,
            totalCorrect,
            totalAnswered == 0 ? 0 : Math.Round(100.0 * totalCorrect / totalAnswered, 1),
            totalAnswered == 0 ? 0 : Math.Round(answers.Average(x => x.TimeSpentSeconds), 1),
            exams.Count,
            exams.Count(e => e.PassScore != null && e.Score >= e.PassScore),
            exams.Count == 0 ? null : exams.Max(e => e.Score),
            exams.Count == 0 ? null : Math.Round(exams.Average(e => e.Score), 1),
            currentStreak,
            longestStreak,
            await attempts.DueQuery(targetId, now).CountAsync(ct),
            trend,
            topics,
            activity,
            topWrong,
            inProgress.Select(AttemptsController.ToSummary).ToList(),
            recent.Select(AttemptsController.ToSummary).ToList());
    }

    /// <summary>Chuỗi ngày học liên tiếp hiện tại (tính đến hôm nay hoặc hôm qua) và dài nhất.</summary>
    private static (int Current, int Longest) Streaks(IEnumerable<DateOnly> days, DateOnly today)
    {
        var set = days.ToHashSet();
        var current = 0;
        var cursor = set.Contains(today) ? today : today.AddDays(-1);
        while (set.Contains(cursor))
        {
            current++;
            cursor = cursor.AddDays(-1);
        }

        int longest = 0, run = 0;
        DateOnly? previous = null;
        foreach (var day in set.Order())
        {
            run = previous is { } p && p.AddDays(1) == day ? run + 1 : 1;
            longest = Math.Max(longest, run);
            previous = day;
        }
        return (current, longest);
    }
}
