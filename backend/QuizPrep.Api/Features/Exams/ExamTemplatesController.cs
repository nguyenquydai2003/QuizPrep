using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizPrep.Api.Common;
using QuizPrep.Api.Data;
using QuizPrep.Api.Domain;
using QuizPrep.Api.Features.Attempts;

namespace QuizPrep.Api.Features.Exams;

[ApiController]
[Route("api/exam-templates")]
[Authorize]
public class ExamTemplatesController(AppDbContext db, QuestionPicker picker) : ControllerBase
{
    [Authorize(Roles = Roles.Admin)]
    [HttpGet]
    public async Task<IReadOnlyList<ExamTemplateDto>> GetAll(CancellationToken ct)
    {
        var templates = await db.ExamTemplates.AsNoTracking().Include(t => t.Rules)
            .OrderByDescending(t => t.UpdatedAt).ToListAsync(ct);
        var names = await LoadNamesAsync(ct);
        var result = new List<ExamTemplateDto>();
        foreach (var t in templates) result.Add(await ToDtoAsync(t, names, ct));
        return result;
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ExamTemplateDto>> Get(int id, CancellationToken ct)
    {
        var template = await db.ExamTemplates.AsNoTracking().Include(t => t.Rules).FirstOrDefaultAsync(t => t.Id == id, ct);
        return template is null ? NotFound() : await ToDtoAsync(template, await LoadNamesAsync(ct), ct);
    }

    /// <summary>Đề thi đã phát hành, kèm kết quả của người đang đăng nhập.</summary>
    [HttpGet("available")]
    public async Task<IReadOnlyList<AvailableExamDto>> GetAvailable(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var templates = await db.ExamTemplates.AsNoTracking()
            .Where(t => t.IsPublished)
            .OrderBy(t => t.Title)
            .Select(t => new { t.Id, t.Title, t.Description, t.DurationMinutes, t.PassScore, Total = t.Rules.Sum(r => r.QuestionCount) })
            .ToListAsync(ct);

        var results = await db.Attempts.AsNoTracking()
            .Where(a => a.UserId == userId && a.Mode == AttemptMode.Exam && a.Status == AttemptStatus.Submitted && a.ExamTemplateId != null)
            .Select(a => new { a.ExamTemplateId, a.Score, a.SubmittedAt })
            .ToListAsync(ct);

        return templates.Select(t =>
        {
            var mine = results.Where(r => r.ExamTemplateId == t.Id).OrderByDescending(r => r.SubmittedAt).ToList();
            return new AvailableExamDto(t.Id, t.Title, t.Description, t.DurationMinutes, t.PassScore, t.Total,
                mine.Count, mine.Max(r => r.Score), mine.FirstOrDefault()?.Score);
        }).ToList();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<ActionResult<ExamTemplateDto>> Create(SaveExamTemplateRequest request, CancellationToken ct)
    {
        var template = new ExamTemplate { Title = request.Title.Trim() };
        await ApplyAsync(template, request, ct);
        db.ExamTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(template, await LoadNamesAsync(ct), ct);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ExamTemplateDto>> Update(int id, SaveExamTemplateRequest request, CancellationToken ct)
    {
        var template = await db.ExamTemplates.Include(t => t.Rules).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return NotFound();

        await ApplyAsync(template, request, ct);
        template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(template, await LoadNamesAsync(ct), ct);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await db.ExamTemplates.Where(t => t.Id == id).ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound() : NoContent();
    }

    private async Task ApplyAsync(ExamTemplate template, SaveExamTemplateRequest request, CancellationToken ct)
    {
        var topicIds = request.Rules.Where(r => r.TopicId != null).Select(r => r.TopicId!.Value).Distinct().ToList();
        var topics = await db.Topics.AsNoTracking().Where(t => topicIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.SubjectId, ct);
        var subjectIds = request.Rules.Where(r => r.TopicId == null).Select(r => r.SubjectId!.Value).Distinct().ToList();
        var existingSubjects = await db.Subjects.AsNoTracking().Where(s => subjectIds.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct);

        if (topics.Count != topicIds.Count || existingSubjects.Count != subjectIds.Count)
            throw new AppProblemException("Ma trận có môn hoặc chủ đề không tồn tại.");

        template.Title = request.Title.Trim();
        template.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        template.DurationMinutes = request.DurationMinutes;
        template.PassScore = request.PassScore;
        template.ShuffleOptions = request.ShuffleOptions;
        template.IsPublished = request.IsPublished;
        template.Rules.Clear();
        template.Rules.AddRange(request.Rules.Select((r, i) => new ExamTemplateRule
        {
            // Chọn chủ đề thì lưu luôn môn của chủ đề cho đồng bộ
            SubjectId = r.TopicId is { } topicId ? topics[topicId] : r.SubjectId,
            TopicId = r.TopicId,
            Difficulty = r.Difficulty,
            QuestionCount = r.QuestionCount,
            SortOrder = i,
        }));
    }

    private record Names(Dictionary<int, string> Subjects, Dictionary<int, string> Topics);

    private async Task<Names> LoadNamesAsync(CancellationToken ct) => new(
        await db.Subjects.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name, ct),
        await db.Topics.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name, ct));

    private async Task<ExamTemplateDto> ToDtoAsync(ExamTemplate t, Names names, CancellationToken ct)
    {
        var rules = new List<ExamRuleDto>();
        foreach (var r in t.Rules.OrderBy(r => r.SortOrder))
        {
            var available = await picker.CountAsync(r.SubjectId, r.TopicId, r.Difficulty, ct);
            rules.Add(new ExamRuleDto(r.Id, r.SubjectId, r.SubjectId is { } s ? names.Subjects.GetValueOrDefault(s) : null,
                r.TopicId, r.TopicId is { } tp ? names.Topics.GetValueOrDefault(tp) : null,
                r.Difficulty, r.QuestionCount, available));
        }
        // Ước lượng: mỗi dòng đủ câu (chưa tính trường hợp các dòng chồng lấn nhau)
        var enough = rules.All(r => r.AvailableCount >= r.QuestionCount);
        return new ExamTemplateDto(t.Id, t.Title, t.Description, t.DurationMinutes, t.PassScore, t.ShuffleOptions,
            t.IsPublished, rules.Sum(r => r.QuestionCount), enough, rules, t.UpdatedAt);
    }
}
