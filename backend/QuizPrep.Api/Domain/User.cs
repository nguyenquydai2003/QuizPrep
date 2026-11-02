namespace QuizPrep.Api.Domain;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserName { get; set; }
    public string PasswordHash { get; set; } = "";
    public required string DisplayName { get; set; }
    public string Role { get; set; } = Roles.User;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
