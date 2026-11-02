using Microsoft.AspNetCore.Diagnostics;

namespace QuizPrep.Api.Common;

/// <summary>Lỗi nghiệp vụ, trả về client dạng ProblemDetails với thông báo thân thiện.</summary>
public class AppProblemException(string title, int statusCode = StatusCodes.Status400BadRequest) : Exception(title)
{
    public int StatusCode { get; } = statusCode;

    public static AppProblemException NotFound(string title = "Không tìm thấy.") => new(title, StatusCodes.Status404NotFound);
    public static AppProblemException Conflict(string title) => new(title, StatusCodes.Status409Conflict);
}

public class AppProblemExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not AppProblemException problem) return false;

        context.Response.StatusCode = problem.StatusCode;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = { Title = problem.Message, Status = problem.StatusCode },
        });
    }
}
