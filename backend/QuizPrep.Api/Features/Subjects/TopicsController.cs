using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizPrep.Api.Data;
using QuizPrep.Api.Domain;

namespace QuizPrep.Api.Features.Subjects;

[ApiController]
[Authorize(Roles = Roles.Admin)]
public class TopicsController(AppDbContext db) : ControllerBase
{
    [HttpPost("api/subjects/{subjectId:int}/topics")]
    public async Task<ActionResult<TopicDto>> Create(int subjectId, SaveTopicRequest request, CancellationToken ct)
    {
        if (!await db.Subjects.AnyAsync(s => s.Id == subjectId, ct)) return NotFound();

        var name = request.Name.Trim();
        if (await db.Topics.AnyAsync(t => t.SubjectId == subjectId && t.Name == name, ct))
            return Problem(title: $"Chủ đề \"{name}\" đã tồn tại trong môn này.", statusCode: StatusCodes.Status409Conflict);

        var topic = new Topic { SubjectId = subjectId, Name = name, SortOrder = request.SortOrder };
        db.Topics.Add(topic);
        await db.SaveChangesAsync(ct);
        return new TopicDto(topic.Id, topic.Name, topic.SortOrder, 0);
    }

    [HttpPut("api/topics/{id:int}")]
    public async Task<IActionResult> Update(int id, SaveTopicRequest request, CancellationToken ct)
    {
        var topic = await db.Topics.FindAsync([id], ct);
        if (topic is null) return NotFound();

        var name = request.Name.Trim();
        if (await db.Topics.AnyAsync(t => t.Id != id && t.SubjectId == topic.SubjectId && t.Name == name, ct))
            return Problem(title: $"Chủ đề \"{name}\" đã tồn tại trong môn này.", statusCode: StatusCodes.Status409Conflict);

        topic.Name = name;
        topic.SortOrder = request.SortOrder;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("api/topics/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var topic = await db.Topics.FindAsync([id], ct);
        if (topic is null) return NotFound();

        if (await db.Questions.AnyAsync(q => q.TopicId == id, ct))
            return Problem(title: "Chủ đề này vẫn còn câu hỏi, hãy xóa hoặc chuyển câu hỏi trước.", statusCode: StatusCodes.Status409Conflict);

        db.Topics.Remove(topic);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
