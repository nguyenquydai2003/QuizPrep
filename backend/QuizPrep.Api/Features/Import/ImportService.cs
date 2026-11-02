using Microsoft.EntityFrameworkCore;
using QuizPrep.Api.Data;
using QuizPrep.Api.Domain;
using QuizPrep.Api.Features.Questions;

namespace QuizPrep.Api.Features.Import;

public class ImportService(AppDbContext db)
{
    private const int MaxNameLength = 200;

    /// <summary>Chuyển dữ liệu thô từ file thành câu hỏi chuẩn hóa (xác định đáp án đúng, độ khó, loại).</summary>
    public static ImportItemDto FromParsed(ParsedQuestion p, int index)
    {
        var item = new ImportItemDto
        {
            Index = index,
            Source = p.Source,
            SubjectName = NullIfEmpty(p.SubjectName),
            TopicName = NullIfEmpty(p.TopicName),
            Content = p.Content,
            Explanation = NullIfEmpty(p.Explanation),
            Tags = QuestionRules.NormalizeTags(p.Tags),
            Warnings = [.. p.Warnings],
        };

        var correct = ResolveCorrect(p, item.Warnings);
        item.Options = p.Options.Select(o => new SaveQuestionOptionRequest(o.Content, correct.Contains(o.Letter))).ToList();

        var difficulty = ImportText.ParseDifficulty(p.DifficultyText);
        if (difficulty == null && !string.IsNullOrWhiteSpace(p.DifficultyText))
            item.Warnings.Add($"Không hiểu độ khó \"{p.DifficultyText}\", dùng mặc định Trung bình.");
        item.Difficulty = difficulty ?? Difficulty.Medium;

        // Không ghi loại thì tự suy ra từ số đáp án đúng
        item.Type = ImportText.ParseType(p.TypeText)
            ?? (correct.Count > 1 ? QuestionType.MultipleChoice : QuestionType.SingleChoice);
        return item;
    }

    private static HashSet<char> ResolveCorrect(ParsedQuestion p, List<string> warnings)
    {
        var letters = p.Options.Select(o => o.Letter).ToHashSet();

        if (!string.IsNullOrWhiteSpace(p.AnswerText))
        {
            var answer = ImportText.ParseAnswerLetters(p.AnswerText);
            if (answer.Count == 0)
                warnings.Add($"Không hiểu đáp án \"{p.AnswerText}\" (cần dạng A hoặc A,C).");
            foreach (var missing in answer.Where(l => !letters.Contains(l)))
                warnings.Add($"Đáp án {missing} không có trong danh sách phương án.");
            return answer.Where(letters.Contains).ToHashSet();
        }

        var marked = p.Options.Where(o => o.Marked).Select(o => o.Letter).ToHashSet();
        if (marked.Count > 0) return marked;

        // Chỉ dùng định dạng khi phân biệt được (không phải tất cả đáp án đều in đậm)
        var emphasized = p.Options.Where(o => o.Emphasized).Select(o => o.Letter).ToHashSet();
        if (emphasized.Count > 0 && emphasized.Count < p.Options.Count)
        {
            warnings.Add("Đáp án đúng được xác định theo định dạng (in đậm/gạch chân/tô màu), hãy kiểm tra lại.");
            return emphasized;
        }
        return [];
    }

    /// <summary>
    /// Kiểm tra hợp lệ, xác định chủ đề (có sẵn hoặc sẽ tạo mới), phát hiện trùng với ngân hàng và trong file.
    /// </summary>
    public async Task<List<ImportItemDto>> AnalyzeAsync(List<ImportItemDto> items, int? defaultTopicId, CancellationToken ct)
    {
        var subjects = await db.Subjects.AsNoTracking().Include(s => s.Topics).ToListAsync(ct);
        var defaultTopic = ResolveDefaultTopic(subjects, defaultTopicId);
        var defaultSubject = defaultTopic == null ? null : subjects.First(s => s.Id == defaultTopic.SubjectId);

        var hashes = items.Select(Hash).ToList();
        var distinctHashes = hashes.Distinct().ToList();
        var existing = await db.Questions.AsNoTracking()
            .Where(q => distinctHashes.Contains(q.ContentHash))
            .Select(q => new { q.Id, q.ContentHash })
            .ToListAsync(ct);
        var existingByHash = existing.GroupBy(x => x.ContentHash).ToDictionary(g => g.Key, g => g.First().Id);
        var seenInFile = new Dictionary<string, int>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            item.Errors = [];
            item.TopicId = null;
            item.CreatesSubject = item.CreatesTopic = false;
            item.DuplicateOfQuestionId = item.DuplicateOfIndex = null;
            item.SubjectName = NullIfEmpty(item.SubjectName);
            item.TopicName = NullIfEmpty(item.TopicName);
            item.Tags = QuestionRules.NormalizeTags(item.Tags);

            item.Errors.AddRange(QuestionRules.Validate(item.Type, item.Content,
                item.Options.Select(o => o.Content).ToList(), item.Options.Count(o => o.IsCorrect)));
            if (item.Tags.Any(t => t.Length > QuestionRules.MaxTagLength))
                item.Errors.Add($"Mỗi tag tối đa {QuestionRules.MaxTagLength} ký tự.");

            ResolveTopic(item, subjects, defaultSubject, defaultTopic);

            if (existingByHash.TryGetValue(hashes[i], out var existingId)) item.DuplicateOfQuestionId = existingId;
            else if (seenInFile.TryGetValue(hashes[i], out var firstIndex)) item.DuplicateOfIndex = firstIndex;
            else seenInFile[hashes[i]] = item.Index;
        }
        return items;
    }

    public async Task<ImportCommitResponse> CommitAsync(ImportCommitRequest request, CancellationToken ct)
    {
        var items = await AnalyzeAsync(request.Items, request.DefaultTopicId, ct);

        // Môn/chủ đề tạo mới được dùng chung giữa các câu cùng tên
        var subjects = await db.Subjects.Include(s => s.Topics).ToListAsync(ct);
        var skipped = new List<string>();
        int created = 0, subjectsCreated = 0, topicsCreated = 0;

        foreach (var item in items)
        {
            if (item.Errors.Count > 0)
            {
                skipped.Add($"{item.Source}: {string.Join(" ", item.Errors)}");
                continue;
            }
            if (item.DuplicateOfQuestionId != null || item.DuplicateOfIndex != null)
            {
                skipped.Add(item.DuplicateOfQuestionId != null
                    ? $"{item.Source}: trùng câu #{item.DuplicateOfQuestionId} đã có."
                    : $"{item.Source}: trùng với câu thứ {item.DuplicateOfIndex} trong file.");
                continue;
            }

            Topic topic;
            if (item.TopicId is { } topicId)
            {
                topic = subjects.SelectMany(s => s.Topics).First(t => t.Id == topicId);
            }
            else
            {
                var subject = FindByName(subjects, item.SubjectName!, s => s.Name);
                if (subject == null)
                {
                    subject = new Subject { Name = item.SubjectName!.Trim(), SortOrder = subjects.Count + 1 };
                    subjects.Add(subject);
                    db.Subjects.Add(subject);
                    subjectsCreated++;
                }
                topic = FindByName(subject.Topics, item.TopicName!, t => t.Name)!;
                if (topic == null)
                {
                    topic = new Topic { Name = item.TopicName!.Trim(), SortOrder = subject.Topics.Count + 1, Subject = subject };
                    subject.Topics.Add(topic);
                    topicsCreated++;
                }
            }

            topic.Questions.Add(new Question
            {
                Type = item.Type,
                Difficulty = item.Difficulty,
                Content = item.Content.Trim(),
                Explanation = NullIfEmpty(item.Explanation),
                Tags = item.Tags,
                ContentHash = Hash(item),
                Options = item.Options.Select((o, i) => new QuestionOption
                {
                    Content = o.Content.Trim(),
                    IsCorrect = o.IsCorrect,
                    SortOrder = i,
                }).ToList(),
            });
            created++;
        }

        await db.SaveChangesAsync(ct);
        return new ImportCommitResponse(created, skipped.Count, subjectsCreated, topicsCreated, skipped);
    }

    private static Topic? ResolveDefaultTopic(List<Subject> subjects, int? defaultTopicId)
    {
        if (defaultTopicId == null) return null;
        return subjects.SelectMany(s => s.Topics).FirstOrDefault(t => t.Id == defaultTopicId)
            ?? throw new ImportFormatException("Chủ đề mặc định không tồn tại.");
    }

    private static void ResolveTopic(ImportItemDto item, List<Subject> subjects, Subject? defaultSubject, Topic? defaultTopic)
    {
        if (item.SubjectName == null && item.TopicName == null)
        {
            if (defaultTopic == null)
                item.Errors.Add("Câu không ghi môn/chủ đề và chưa chọn chủ đề mặc định.");
            else
            {
                item.SubjectName = defaultSubject!.Name;
                item.TopicName = defaultTopic.Name;
                item.TopicId = defaultTopic.Id;
            }
            return;
        }

        // Chỉ ghi chủ đề => thuộc môn của chủ đề mặc định
        item.SubjectName ??= defaultSubject?.Name;
        if (item.SubjectName == null)
        {
            item.Errors.Add("Thiếu tên môn (hoặc chọn chủ đề mặc định để dùng môn của nó).");
            return;
        }
        if (item.TopicName == null)
        {
            item.Errors.Add("Thiếu tên chủ đề.");
            return;
        }
        if (item.SubjectName.Length > MaxNameLength || item.TopicName.Length > MaxNameLength)
        {
            item.Errors.Add($"Tên môn/chủ đề tối đa {MaxNameLength} ký tự.");
            return;
        }

        var subject = FindByName(subjects, item.SubjectName, s => s.Name);
        var topic = subject == null ? null : FindByName(subject.Topics, item.TopicName, t => t.Name);
        item.CreatesSubject = subject == null;
        item.CreatesTopic = topic == null;
        item.TopicId = topic?.Id;
    }

    private static T? FindByName<T>(IEnumerable<T> source, string name, Func<T, string> getName) where T : class =>
        source.FirstOrDefault(x => string.Equals(getName(x).Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string Hash(ImportItemDto item) =>
        QuestionHasher.Compute(item.Content, item.Options.Select(o => o.Content));

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
