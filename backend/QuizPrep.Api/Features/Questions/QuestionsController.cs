using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizPrep.Api.Common;
using QuizPrep.Api.Data;
using QuizPrep.Api.Domain;

namespace QuizPrep.Api.Features.Questions;

/// <summary>Quản lý ngân hàng câu hỏi (trả về cả đáp án đúng nên chỉ dành cho Admin).</summary>
[ApiController]
[Route("api/questions")]
[Authorize(Roles = Roles.Admin)]
public class QuestionsController(AppDbContext db) : ControllerBase
{
    private const int PreviewLength = 300;

    [HttpGet]
    public async Task<PagedResult<QuestionListItemDto>> Search([FromQuery] QuestionQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.Questions.AsNoTracking();
        if (query.TopicId is not null) q = q.Where(x => x.TopicId == query.TopicId);
        else if (query.SubjectId is not null) q = q.Where(x => x.Topic.SubjectId == query.SubjectId);
        if (query.Difficulty is not null) q = q.Where(x => x.Difficulty == query.Difficulty);
        if (query.Type is not null) q = q.Where(x => x.Type == query.Type);
        if (!string.IsNullOrWhiteSpace(query.Search)) q = q.Where(x => x.Content.Contains(query.Search.Trim()));
        if (!string.IsNullOrWhiteSpace(query.Tag)) q = q.Where(x => x.Tags.Contains(query.Tag.Trim()));

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new QuestionListItemDto(
                x.Id, x.TopicId, x.Topic.Name, x.Topic.Subject.Name, x.Type, x.Difficulty,
                x.Content.Substring(0, PreviewLength), x.Tags, x.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResult<QuestionListItemDto>(items, total, page, pageSize);
    }

    [HttpGet("tags")]
    public async Task<IReadOnlyList<string>> GetTags(CancellationToken ct) =>
        await db.Questions.SelectMany(x => x.Tags).Distinct().OrderBy(t => t).ToListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<QuestionDetailDto>> Get(int id, CancellationToken ct)
    {
        var question = await db.Questions.AsNoTracking()
            .Include(x => x.Topic)
            .Include(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return question is null ? NotFound() : ToDetail(question);
    }

    [HttpPost]
    public async Task<ActionResult<QuestionDetailDto>> Create(SaveQuestionRequest request, CancellationToken ct)
    {
        var topic = await db.Topics.FindAsync([request.TopicId], ct);
        if (topic is null) return TopicNotFound();

        var hash = QuestionHasher.Compute(request.Content, request.Options.Select(o => o.Content));
        if (await FindDuplicateAsync(hash, excludeId: null, ct) is { } dupId) return Duplicate(dupId);

        var question = new Question { Content = request.Content.Trim() };
        Apply(question, request, hash);
        db.Questions.Add(question);
        await db.SaveChangesAsync(ct);

        question.Topic = topic;
        return CreatedAtAction(nameof(Get), new { id = question.Id }, ToDetail(question));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<QuestionDetailDto>> Update(int id, SaveQuestionRequest request, CancellationToken ct)
    {
        var question = await db.Questions.Include(x => x.Options).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (question is null) return NotFound();

        var topic = await db.Topics.FindAsync([request.TopicId], ct);
        if (topic is null) return TopicNotFound();

        var hash = QuestionHasher.Compute(request.Content, request.Options.Select(o => o.Content));
        if (await FindDuplicateAsync(hash, excludeId: id, ct) is { } dupId) return Duplicate(dupId);

        Apply(question, request, hash);
        question.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        question.Topic = topic;
        return ToDetail(question);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await db.Questions.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound() : NoContent();
    }

    private static void Apply(Question question, SaveQuestionRequest request, string hash)
    {
        question.TopicId = request.TopicId;
        question.Type = request.Type;
        question.Difficulty = request.Difficulty;
        question.Content = request.Content.Trim();
        question.Explanation = string.IsNullOrWhiteSpace(request.Explanation) ? null : request.Explanation.Trim();
        question.Tags = request.NormalizedTags();
        question.ContentHash = hash;

        // Thay toàn bộ đáp án; lịch sử làm bài sẽ lưu snapshot nên không phụ thuộc Id đáp án.
        question.Options.Clear();
        question.Options.AddRange(request.Options.Select((o, i) => new QuestionOption
        {
            Content = o.Content.Trim(),
            IsCorrect = o.IsCorrect,
            SortOrder = i,
        }));
    }

    private async Task<int?> FindDuplicateAsync(string hash, int? excludeId, CancellationToken ct) =>
        await db.Questions
            .Where(x => x.ContentHash == hash && x.Id != excludeId)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);

    private ObjectResult TopicNotFound() =>
        Problem(title: "Chủ đề không tồn tại.", statusCode: StatusCodes.Status400BadRequest);

    private ObjectResult Duplicate(int existingId) =>
        Problem(title: $"Câu hỏi này đã tồn tại (ID {existingId}).", statusCode: StatusCodes.Status409Conflict);

    private static QuestionDetailDto ToDetail(Question q) => new(
        q.Id, q.Topic.SubjectId, q.TopicId, q.Type, q.Difficulty, q.Content, q.Explanation, q.Tags,
        q.Options.OrderBy(o => o.SortOrder).Select(o => new QuestionOptionDto(o.Id, o.Content, o.IsCorrect, o.SortOrder)).ToList(),
        q.CreatedAt, q.UpdatedAt);
}
