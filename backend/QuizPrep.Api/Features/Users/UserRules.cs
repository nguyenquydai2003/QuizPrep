using QuizPrep.Api.Domain;

namespace QuizPrep.Api.Features.Users;

public static class UserRules
{
    public const string UserNamePattern = @"^[a-zA-Z0-9._-]{3,50}$";
    public const string UserNameMessage = "Tên đăng nhập 3-50 ký tự, chỉ gồm chữ không dấu, số và . _ -";
    public const int MinPasswordLength = 6;
    public const string PasswordMessage = "Mật khẩu tối thiểu 6 ký tự.";

    public static bool IsValidRole(string role) => role is Roles.Admin or Roles.User;
}
