using Microsoft.EntityFrameworkCore;
using QuizPrep.Api.Data;
using QuizPrep.Api.Domain;

namespace QuizPrep.Api.Features.Attempts;

/// <summary>Lọc và bốc ngẫu nhiên câu hỏi, dùng cho luyện tập và sinh đề thi theo ma trận.</summary>
public class QuestionPicker(AppDbContext db)
{
    public IQueryable<Question> Filter(int? subjectId, int? topicId, Difficulty? difficulty)
    {
        var query = db.Questions.AsQueryable();
        if (topicId != null) query = query.Where(q => q.TopicId == topicId);
        else if (subjectId != null) query = query.Where(q => q.Topic.SubjectId == subjectId);
        if (difficulty != null) query = query.Where(q => q.Difficulty == difficulty);
        return query;
    }

    public Task<int> CountAsync(int? subjectId, int? topicId, Difficulty? difficulty, CancellationToken ct) =>
        Filter(subjectId, topicId, difficulty).CountAsync(ct);

    /// <summary>Bốc ngẫu nhiên tối đa <paramref name="count"/> câu (ORDER BY NEWID()), bỏ qua các câu đã chọn.</summary>
    public async Task<List<Question>> PickRandomAsync(IQueryable<Question> query, int count, ICollection<int> excludeIds, CancellationToken ct)
    {
        // Bốc Id trước rồi mới tải chi tiết: nếu Include + split query trực tiếp trên ORDER BY NEWID(),
        // mỗi truy vấn con sẽ bốc ngẫu nhiên một tập khác nhau và đáp án không khớp câu hỏi.
        var ids = await query.Where(q => !excludeIds.Contains(q.Id))
            .OrderBy(_ => Guid.NewGuid())
            .Select(q => q.Id)
            .Take(count)
            .ToListAsync(ct);
        return await LoadAsync(ids, ct);
    }

    /// <summary>Tải câu hỏi kèm đáp án và chủ đề, giữ nguyên thứ tự của <paramref name="ids"/>.</summary>
    public async Task<List<Question>> LoadAsync(List<int> ids, CancellationToken ct)
    {
        var questions = await db.Questions.Where(q => ids.Contains(q.Id))
            .Include(q => q.Options)
            .Include(q => q.Topic).ThenInclude(t => t.Subject)
            .AsSplitQuery()
            .ToListAsync(ct);
        return questions.OrderBy(q => ids.IndexOf(q.Id)).ToList();
    }
}
