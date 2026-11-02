using Microsoft.EntityFrameworkCore;
using QuizPrep.Api.Domain;

namespace QuizPrep.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<ExamTemplate> ExamTemplates => Set<ExamTemplate>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<AttemptQuestion> AttemptQuestions => Set<AttemptQuestion>();
    public DbSet<UserQuestionStat> UserQuestionStats => Set<UserQuestionStat>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Mọi DateTime đều lưu UTC; đánh dấu Kind khi đọc ra để JSON có hậu tố "Z".
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.Property(x => x.UserName).HasMaxLength(50);
            e.HasIndex(x => x.UserName).IsUnique();
            e.Property(x => x.DisplayName).HasMaxLength(100);
            e.Property(x => x.Role).HasMaxLength(20);
            e.Property(x => x.PasswordHash).HasMaxLength(200);
        });

        b.Entity<Subject>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Description).HasMaxLength(1000);
        });

        b.Entity<Topic>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasIndex(x => new { x.SubjectId, x.Name }).IsUnique();
            e.HasOne(x => x.Subject).WithMany(x => x.Topics).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Question>(e =>
        {
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.ContentHash).HasMaxLength(64).IsFixedLength();
            e.HasIndex(x => x.ContentHash);
            // Không cho xóa chủ đề khi còn câu hỏi
            e.HasOne(x => x.Topic).WithMany(x => x.Questions).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Options).WithOne().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ExamTemplate>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasMany(x => x.Rules).WithOne().HasForeignKey(x => x.ExamTemplateId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ExamTemplateRule>(e =>
        {
            e.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(10);
        });

        b.Entity<Attempt>(e =>
        {
            e.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Title).HasMaxLength(300);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            // Xóa đề thi vẫn giữ lịch sử bài làm
            e.HasOne<ExamTemplate>().WithMany().HasForeignKey(x => x.ExamTemplateId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Questions).WithOne().HasForeignKey(x => x.AttemptId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.Status, x.StartedAt });
        });

        b.Entity<AttemptQuestion>(e =>
        {
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(10);
            e.Property(x => x.SubjectName).HasMaxLength(200);
            e.Property(x => x.TopicName).HasMaxLength(200);
            e.OwnsMany(x => x.Options, o => o.ToJson());
            // Xóa câu hỏi gốc vẫn giữ bản chụp trong bài làm
            e.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.AttemptId, x.Order });
        });

        b.Entity<UserQuestionStat>(e =>
        {
            e.HasKey(x => new { x.UserId, x.QuestionId });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.NextReviewAt });
        });
    }
}
