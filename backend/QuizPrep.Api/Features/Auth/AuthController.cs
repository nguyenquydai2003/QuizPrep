using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizPrep.Api.Common;
using QuizPrep.Api.Data;
using QuizPrep.Api.Domain;
using QuizPrep.Api.Features.Users;

namespace QuizPrep.Api.Features.Auth;

public record LoginRequest([Required] string UserName, [Required] string Password);

public record RegisterRequest(
    [Required, RegularExpression(UserRules.UserNamePattern, ErrorMessage = UserRules.UserNameMessage)] string UserName,
    [Required(ErrorMessage = "Nhập tên hiển thị."), MaxLength(100)] string DisplayName,
    [Required, MinLength(UserRules.MinPasswordLength, ErrorMessage = UserRules.PasswordMessage)] string Password);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(UserRules.MinPasswordLength, ErrorMessage = UserRules.PasswordMessage)] string NewPassword);

public record UpdateProfileRequest([Required(ErrorMessage = "Nhập tên hiển thị."), MaxLength(100)] string DisplayName);

public record UserDto(Guid Id, string UserName, string DisplayName, string Role);

public record LoginResponse(string Token, DateTime ExpiresAt, UserDto User);

public record AuthOptionsDto(bool AllowRegistration);

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, IPasswordHasher<User> hasher, JwtTokenService tokens, IConfiguration config) : ControllerBase
{
    private bool AllowRegistration => config.GetValue<bool>("Auth:AllowRegistration");

    [HttpGet("options")]
    public AuthOptionsDto Options() => new(AllowRegistration);

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == request.UserName.Trim(), ct);
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Problem(title: "Sai tên đăng nhập hoặc mật khẩu.", statusCode: StatusCodes.Status401Unauthorized);

        return CreateLoginResponse(user);
    }

    [HttpPost("register")]
    public async Task<ActionResult<LoginResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        if (!AllowRegistration)
            return Problem(title: "Chức năng đăng ký đang tắt. Hãy liên hệ quản trị viên.", statusCode: StatusCodes.Status403Forbidden);

        var userName = request.UserName.Trim();
        if (await db.Users.AnyAsync(u => u.UserName == userName, ct))
            return Problem(title: "Tên đăng nhập đã được sử dụng.", statusCode: StatusCodes.Status409Conflict);

        var user = new User { UserName = userName, DisplayName = request.DisplayName.Trim(), Role = Roles.User };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return CreateLoginResponse(user);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == User.GetUserId(), ct);
        return user is null ? Unauthorized() : ToDto(user);
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<ActionResult<UserDto>> UpdateProfile(UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == User.GetUserId(), ct);
        if (user is null) return Unauthorized();
        user.DisplayName = request.DisplayName.Trim();
        await db.SaveChangesAsync(ct);
        return ToDto(user);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == User.GetUserId(), ct);
        if (user is null) return Unauthorized();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            return Problem(title: "Mật khẩu hiện tại không đúng.", statusCode: StatusCodes.Status400BadRequest);

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private LoginResponse CreateLoginResponse(User user)
    {
        var (token, expiresAt) = tokens.CreateToken(user);
        return new LoginResponse(token, expiresAt, ToDto(user));
    }

    public static UserDto ToDto(User u) => new(u.Id, u.UserName, u.DisplayName, u.Role);
}
