using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizPrep.Api.Common;
using QuizPrep.Api.Data;
using QuizPrep.Api.Domain;

namespace QuizPrep.Api.Features.Attempts;

[ApiController]
[Route("api/attempts")]
[Authorize]
public class AttemptsController(AppDbContext db, AttemptService attempts) : ControllerBase
{
    [HttpPost("practice")]
    public async Task<ActionResult<AttemptDto>> StartPractice(PracticeRequest request, CancellationToken ct) =>
        Created(await attempts.CreatePracticeAsync(User.GetUserId(), request, ct));

    [HttpPost("exam")]
    public async Task<ActionResult<AttemptDto>> StartExam(StartExamRequest request, CancellationToken ct) =>
        Created(await attempts.CreateExamAsync(User.GetUserId(), request.ExamTemplateId, User.IsInRole(Roles.Admin), ct));

    [HttpPost("review")]
    public async Task<ActionResult<AttemptDto>> StartReview(ReviewRequest request, CancellationToken ct) =>
        Created(await attempts.CreateReviewAsync(User.GetUserId(), request, ct));

    [HttpGet("review-summary")]
    public Task<ReviewSummaryDto> ReviewSummary(CancellationToken ct) =>
        attempts.GetReviewSummaryAsync(User.GetUserId(), ct);

    [HttpGet]
    public async Task<PagedResult<AttemptSummaryDto>> List(
        [FromQuery] AttemptMode? mode, [FromQuery] AttemptStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var userId = User.GetUserId();

        var query = db.Attempts.AsNoTracking().Where(a => a.UserId == userId);
        if (mode != null) query = query.Where(a => a.Mode == mode);
        if (status != null) query = query.Where(a => a.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.StartedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return new PagedResult<AttemptSummaryDto>(items.Select(ToSummary).ToList(), total, page, pageSize);
    }

    [HttpGet("{id:guid}")]
    public async Task<AttemptDto> Get(Guid id, CancellationToken ct) =>
        AttemptMapper.ToDto(await attempts.GetAsync(User.GetUserId(), id, ct));

    [HttpPut("{id:guid}/questions/{questionId:int}/answer")]
    public async Task<AttemptQuestionDto> Answer(Guid id, int questionId, AnswerRequest request, CancellationToken ct)
    {
        var attempt = await attempts.GetAsync(User.GetUserId(), id, ct);
        var question = await attempts.AnswerAsync(attempt, questionId, request, ct);
        return AttemptMapper.ToDto(attempt, question);
    }

    [HttpPut("{id:guid}/questions/{questionId:int}/flag")]
    public async Task<AttemptQuestionDto> Flag(Guid id, int questionId, FlagRequest request, CancellationToken ct)
    {
        var attempt = await attempts.GetAsync(User.GetUserId(), id, ct);
        var question = await attempts.FlagAsync(attempt, questionId, request.Flagged, ct);
        return AttemptMapper.ToDto(attempt, question);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<AttemptDto> Submit(Guid id, CancellationToken ct)
    {
        var attempt = await attempts.GetAsync(User.GetUserId(), id, ct);
        await attempts.SubmitAsync(attempt, ct);
        return AttemptMapper.ToDto(attempt);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var deleted = await db.Attempts.Where(a => a.Id == id && a.UserId == userId).ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound() : NoContent();
    }

    public static AttemptSummaryDto ToSummary(Attempt a) => new(
        a.Id, a.Mode, a.Status, a.Title, a.StartedAt, a.Deadline, a.SubmittedAt,
        a.TotalQuestions, a.AnsweredCount, a.CorrectCount, a.Score, a.PassScore, AttemptMapper.Passed(a));

    private CreatedAtActionResult Created(Attempt attempt) =>
        CreatedAtAction(nameof(Get), new { id = attempt.Id }, AttemptMapper.ToDto(attempt));
}
