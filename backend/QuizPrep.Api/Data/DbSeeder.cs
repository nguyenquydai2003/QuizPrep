using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuizPrep.Api.Domain;
using QuizPrep.Api.Features.Questions;

namespace QuizPrep.Api.Data;

public static class DbSeeder
{
    public static async Task MigrateAndSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbSeeder));

        await db.Database.MigrateAsync();

        if (!await db.Users.AnyAsync(u => u.Role == Roles.Admin))
        {
            var password = config["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("Chưa cấu hình Seed:AdminPassword, bỏ qua tạo tài khoản admin.");
            }
            else
            {
                var admin = new User
                {
                    UserName = config["Seed:AdminUserName"] ?? "admin",
                    DisplayName = config["Seed:AdminDisplayName"] ?? "Admin",
                    Role = Roles.Admin,
                };
                admin.PasswordHash = hasher.HashPassword(admin, password);
                db.Users.Add(admin);
                logger.LogInformation("Đã tạo tài khoản admin '{UserName}'.", admin.UserName);
            }
        }

        if (!await db.Subjects.AnyAsync())
        {
            db.Subjects.AddRange(SampleSubjects());
            logger.LogInformation("Đã tạo dữ liệu mẫu.");
        }

        await db.SaveChangesAsync();

        if (!await db.ExamTemplates.AnyAsync() && await db.Subjects.AnyAsync())
        {
            // Đề mẫu: lấy ngẫu nhiên từ mỗi môn một số câu
            var rules = await db.Subjects.OrderBy(s => s.SortOrder)
                .Select(s => new { s.Id, Count = s.Topics.Sum(t => t.Questions.Count) })
                .Where(s => s.Count > 0)
                .ToListAsync();
            if (rules.Count > 0)
            {
                db.ExamTemplates.Add(new ExamTemplate
                {
                    Title = "Đề thi thử tổng hợp",
                    Description = "Đề mẫu lấy ngẫu nhiên câu hỏi từ các môn.",
                    DurationMinutes = 10,
                    PassScore = 60,
                    IsPublished = true,
                    Rules = rules.Select((r, i) => new ExamTemplateRule
                    {
                        SubjectId = r.Id,
                        QuestionCount = Math.Min(r.Count, 3),
                        SortOrder = i,
                    }).ToList(),
                });
                await db.SaveChangesAsync();
                logger.LogInformation("Đã tạo đề thi mẫu.");
            }
        }
    }

    private static IEnumerable<Subject> SampleSubjects()
    {
        yield return new Subject
        {
            Name = "Lập trình C#",
            SortOrder = 1,
            Topics =
            [
                new Topic
                {
                    Name = "Cơ bản",
                    SortOrder = 1,
                    Questions =
                    [
                        Sample(Difficulty.Medium, ["toán-tử"],
                            """
                            Đoạn code sau in ra gì?

                            ```csharp
                            int x = 5;
                            Console.WriteLine(x++ + ++x);
                            ```
                            """,
                            "`x++` trả về 5 rồi tăng x lên 6; `++x` tăng x lên 7 rồi trả về 7. Kết quả: 5 + 7 = **12**.",
                            ("10", false), ("11", false), ("12", true), ("13", false)),
                        Sample(Difficulty.Easy, ["kiểu-dữ-liệu"],
                            "Kiểu nào sau đây là **value type** trong C#?",
                            "`struct` là value type; `class`, `string`, `object` đều là reference type.",
                            ("`string`", false), ("`object`", false), ("`struct`", true), ("`class`", false)),
                    ],
                },
                new Topic
                {
                    Name = "LINQ",
                    SortOrder = 2,
                    Questions =
                    [
                        Sample(Difficulty.Hard, ["deferred-execution"],
                            """
                            Kết quả in ra của đoạn code sau là gì?

                            ```csharp
                            var list = new List<int> { 1, 2, 3 };
                            var query = list.Where(n => n > 1);
                            list.Add(4);
                            Console.WriteLine(query.Count());
                            ```
                            """,
                            "LINQ dùng **deferred execution**: query chỉ chạy khi gọi `Count()`, lúc đó list đã có 4 phần tử nên kết quả là 3 (2, 3, 4).",
                            ("2", false), ("3", true), ("4", false), ("Lỗi runtime", false)),
                    ],
                },
            ],
        };

        yield return new Subject
        {
            Name = "SQL",
            SortOrder = 2,
            Topics =
            [
                new Topic
                {
                    Name = "Truy vấn",
                    SortOrder = 1,
                    Questions =
                    [
                        Sample(Difficulty.Medium, ["join"],
                            "Những loại JOIN nào **giữ lại toàn bộ dòng** của bảng bên trái?",
                            "`LEFT JOIN` và `FULL OUTER JOIN` đều giữ toàn bộ dòng bảng trái; `INNER JOIN` chỉ giữ dòng khớp.",
                            QuestionType.MultipleChoice,
                            ("`INNER JOIN`", false), ("`LEFT JOIN`", true), ("`FULL OUTER JOIN`", true), ("`CROSS APPLY`", false)),
                    ],
                },
            ],
        };
    }

    private static Question Sample(Difficulty difficulty, List<string> tags, string content, string explanation,
        params (string Content, bool IsCorrect)[] options) =>
        Sample(difficulty, tags, content, explanation, QuestionType.SingleChoice, options);

    private static Question Sample(Difficulty difficulty, List<string> tags, string content, string explanation,
        QuestionType type, params (string Content, bool IsCorrect)[] options) => new()
    {
        Type = type,
        Difficulty = difficulty,
        Content = content,
        Explanation = explanation,
        Tags = tags,
        ContentHash = QuestionHasher.Compute(content, options.Select(o => o.Content)),
        Options = options.Select((o, i) => new QuestionOption { Content = o.Content, IsCorrect = o.IsCorrect, SortOrder = i }).ToList(),
    };
}
