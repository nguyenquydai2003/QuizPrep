using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizPrep.Api.Common;
using QuizPrep.Api.Data;
using QuizPrep.Api.Domain;

namespace QuizPrep.Api.Features.Users;

public record UserAdminDto(
    Guid Id,
    string UserName,
    string DisplayName,
    string Role,
    DateTime CreatedAt,
    int AttemptCount,
    int AnsweredCount,
    DateTime? LastActivityAt);

public record CreateUserRequest(
    [Required, RegularExpression(UserRules.UserNamePattern, ErrorMessage = UserRules.UserNameMessage)] string UserName,
    [Required(ErrorMessage = "Nhập tên hiển thị."), MaxLength(100)] string DisplayName,
    [Required, MinLength(UserRules.MinPasswordLength, ErrorMessage = UserRules.PasswordMessage)] string Password,
    string Role = Roles.User);

public record UpdateUserRequest([Required(ErrorMessage = "Nhập tên hiển thị."), MaxLength(100)] string DisplayName, string Role);

public record ResetPasswordRequest([Required, MinLength(UserRules.MinPasswordLength, ErrorMessage = UserRules.PasswordMessage)] string NewPassword);

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
public class UsersController(AppDbContext db, IPasswordHasher<User> hasher) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<UserAdminDto>> GetAll(CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new UserAdminDto(
                u.Id, u.UserName, u.DisplayName, u.Role, u.CreatedAt,
                db.Attempts.Count(a => a.UserId == u.Id),
                db.Attempts.Where(a => a.UserId == u.Id).Sum(a => (int?)a.AnsweredCount) ?? 0,
                db.Attempts.Where(a => a.UserId == u.Id).Max(a => (DateTime?)a.StartedAt)))
            .ToListAsync(ct);

    [HttpPost]
    public async Task<ActionResult<UserAdminDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        if (!UserRules.IsValidRole(request.Role)) return InvalidRole();
        var userName = request.UserName.Trim();
        if (await db.Users.AnyAsync(u => u.UserName == userName, ct))
            return Problem(title: "Tên đăng nhập đã được sử dụng.", statusCode: StatusCodes.Status409Conflict);

        var user = new User { UserName = userName, DisplayName = request.DisplayName.Trim(), Role = request.Role };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return new UserAdminDto(user.Id, user.UserName, user.DisplayName, user.Role, user.CreatedAt, 0, 0, null);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        if (!UserRules.IsValidRole(request.Role)) return InvalidRole();
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return NotFound();

        if (user.Role == Roles.Admin && request.Role != Roles.Admin && await IsLastAdminAsync(id, ct))
            return Problem(title: "Không thể hạ quyền quản trị viên cuối cùng.", statusCode: StatusCodes.Status409Conflict);

        user.DisplayName = request.DisplayName.Trim();
        user.Role = request.Role;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return NotFound();
        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (id == User.GetUserId())
            return Problem(title: "Không thể tự xóa tài khoản đang đăng nhập.", statusCode: StatusCodes.Status400BadRequest);

        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return NotFound();
        if (user.Role == Roles.Admin && await IsLastAdminAsync(id, ct))
            return Problem(title: "Không thể xóa quản trị viên cuối cùng.", statusCode: StatusCodes.Status409Conflict);

        // Bài làm và thống kê của người dùng bị xóa theo (cascade)
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<bool> IsLastAdminAsync(Guid id, CancellationToken ct) =>
        db.Users.AllAsync(u => u.Role != Roles.Admin || u.Id == id, ct);

    private ObjectResult InvalidRole() =>
        Problem(title: "Vai trò không hợp lệ.", statusCode: StatusCodes.Status400BadRequest);
}
