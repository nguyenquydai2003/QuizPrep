using System.Text;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using QuizPrep.Api.Common;
using QuizPrep.Api.Domain;

namespace QuizPrep.Api.Features.Ai;

public record AiOptionInput(string Content, bool IsCorrect);

public record AiExplainRequest(QuestionType Type, string Content, List<AiOptionInput> Options);

public record AiExplainResponse(string Explanation);

public record AiStatusDto(bool Enabled, string? Model);

/// <summary>
/// Gợi ý lời giải thích cho câu hỏi bằng Claude. Chỉ bật khi có API key
/// (cấu hình Anthropic:ApiKey hoặc biến môi trường ANTHROPIC_API_KEY).
/// </summary>
public class AiExplanationService(IConfiguration config, ILogger<AiExplanationService> logger)
{
    private const string DefaultModel = "claude-opus-5-5";

    private const string SystemPrompt = """
        Bạn là giảng viên luyện thi trắc nghiệm tuyển dụng (lập trình, CNTT, kiến thức chung).
        Nhiệm vụ: viết lời giải thích ngắn gọn bằng tiếng Việt cho một câu hỏi trắc nghiệm đã biết đáp án đúng.

        Yêu cầu:
        - Giải thích vì sao đáp án đúng là đúng, và nêu ngắn gọn vì sao các phương án còn lại sai (nếu hữu ích).
        - Nếu câu hỏi có code, chỉ ra từng bước code chạy dẫn đến kết quả.
        - Dùng Markdown: `code` trong dòng, khối ```ngôn-ngữ cho code nhiều dòng. Không dùng tiêu đề (#).
        - Không nhắc lại đề bài, không mở đầu kiểu "Đây là lời giải thích". Tối đa khoảng 150 từ.
        - Nếu đáp án được đánh dấu có vẻ sai, vẫn giải thích nhưng thêm một dòng cuối bắt đầu bằng "⚠️ Lưu ý:" nêu nghi ngờ.
        """;

    private string? ApiKey => config["Anthropic:ApiKey"] is { Length: > 0 } key ? key : Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

    private string Model => config["Anthropic:Model"] is { Length: > 0 } model ? model : DefaultModel;

    public AiStatusDto Status() => new(!string.IsNullOrWhiteSpace(ApiKey), string.IsNullOrWhiteSpace(ApiKey) ? null : Model);

    public async Task<string> ExplainAsync(AiExplainRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new AppProblemException("Chưa cấu hình API key của Claude (Anthropic:ApiKey).", StatusCodes.Status503ServiceUnavailable);

        var client = new AnthropicClient { ApiKey = ApiKey };
        try
        {
            BetaMessage response = await client.Beta.Messages.Create(new MessageCreateParams
            {
                Model = Model,
                MaxTokens = 16000,
                System = SystemPrompt,
                OutputConfig = new BetaOutputConfig { Effort = Effort.Medium },
                // Nếu model từ chối (stop_reason "refusal"), server tự chọn model dự phòng theo loại từ chối
                Betas = ["server-side-fallback-2026-07-01"],
                Fallbacks = new Default(),
                Messages = [new BetaUserMessageParam { Content = BuildPrompt(request) }],
            }, cancellationToken: ct);

            if (response.StopReason == "refusal")
                throw new AppProblemException("AI từ chối trả lời câu hỏi này.", StatusCodes.Status422UnprocessableEntity);

            var text = string.Concat(response.Content.Select(b => b.TryPickText(out var t) ? t.Text : "")).Trim();
            if (text.Length == 0)
                throw new AppProblemException("AI không trả về nội dung.", StatusCodes.Status502BadGateway);
            return text;
        }
        catch (AnthropicUnauthorizedException)
        {
            throw new AppProblemException("API key của Claude không hợp lệ.", StatusCodes.Status502BadGateway);
        }
        catch (AnthropicRateLimitException)
        {
            throw new AppProblemException("Đã vượt giới hạn gọi AI, thử lại sau ít phút.", StatusCodes.Status429TooManyRequests);
        }
        catch (AnthropicApiException ex)
        {
            logger.LogWarning(ex, "Gọi Claude API thất bại");
            throw new AppProblemException("Gọi AI thất bại, thử lại sau.", StatusCodes.Status502BadGateway);
        }
        catch (AnthropicIOException ex)
        {
            logger.LogWarning(ex, "Không kết nối được Claude API");
            throw new AppProblemException("Không kết nối được dịch vụ AI.", StatusCodes.Status502BadGateway);
        }
    }

    private static string BuildPrompt(AiExplainRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine(request.Type == QuestionType.MultipleChoice ? "Loại: chọn nhiều đáp án" : "Loại: chọn một đáp án");
        sb.AppendLine().AppendLine("<cau_hoi>").AppendLine(request.Content.Trim()).AppendLine("</cau_hoi>").AppendLine();
        sb.AppendLine("<phuong_an>");
        for (var i = 0; i < request.Options.Count; i++)
            sb.AppendLine($"{(char)('A' + i)}. {request.Options[i].Content.Trim()}");
        sb.AppendLine("</phuong_an>").AppendLine();
        var correct = request.Options.Select((o, i) => (o, i)).Where(x => x.o.IsCorrect).Select(x => (char)('A' + x.i));
        sb.AppendLine($"Đáp án đúng: {string.Join(", ", correct)}");
        return sb.ToString();
    }
}
