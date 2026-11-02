using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizPrep.Api.Data;
using QuizPrep.Api.Domain;

namespace QuizPrep.Api.Features.Subjects;

[ApiController]
[Route("api/subjects")]
[Authorize]
public class SubjectsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<SubjectDto>> GetAll(CancellationToken ct) =>
        await db.Subjects.AsNoTracking()
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new SubjectDto(s.Id, s.Name, s.Description, s.SortOrder,
                s.Topics.OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
                    .Select(t => new TopicDto(t.Id, t.Name, t.SortOrder, t.Questions.Count))
                    .ToList()))
            .ToListAsync(ct);

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<ActionResult<SubjectDto>> Create(SaveSubjectRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (await db.Subjects.AnyAsync(s => s.Name == name, ct))
            return Problem(title: $"Môn \"{name}\" đã tồn tại.", statusCode: StatusCodes.Status409Conflict);

        var subject = new Subject { Name = name, Description = request.Description?.Trim(), SortOrder = request.SortOrder };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync(ct);
        return new SubjectDto(subject.Id, subject.Name, subject.Description, subject.SortOrder, []);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveSubjectRequest request, CancellationToken ct)
    {
        var subject = await db.Subjects.FindAsync([id], ct);
        if (subject is null) return NotFound();

        var name = request.Name.Trim();
        if (await db.Subjects.AnyAsync(s => s.Id != id && s.Name == name, ct))
            return Problem(title: $"Môn \"{name}\" đã tồn tại.", statusCode: StatusCodes.Status409Conflict);

        subject.Name = name;
        subject.Description = request.Description?.Trim();
        subject.SortOrder = request.SortOrder;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var subject = await db.Subjects.FindAsync([id], ct);
        if (subject is null) return NotFound();

        if (await db.Questions.AnyAsync(q => q.Topic.SubjectId == id, ct))
            return Problem(title: "Môn này vẫn còn câu hỏi, hãy xóa hoặc chuyển câu hỏi trước.", statusCode: StatusCodes.Status409Conflict);

        db.Subjects.Remove(subject);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
