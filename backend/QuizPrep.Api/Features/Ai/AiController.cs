using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizPrep.Api.Common;
using QuizPrep.Api.Domain;
using QuizPrep.Api.Features.Questions;

namespace QuizPrep.Api.Features.Ai;

[ApiController]
[Route("api/ai")]
[Authorize(Roles = Roles.Admin)]
public class AiController(AiExplanationService ai) : ControllerBase
{
    [HttpGet("status")]
    public AiStatusDto Status() => ai.Status();

    [HttpPost("explain")]
    public async Task<AiExplainResponse> Explain(AiExplainRequest request, CancellationToken ct)
    {
        var errors = QuestionRules.Validate(request.Type, request.Content,
            request.Options.Select(o => o.Content).ToList(), request.Options.Count(o => o.IsCorrect)).ToList();
        if (errors.Count > 0) throw new AppProblemException(errors[0]);

        return new AiExplainResponse(await ai.ExplainAsync(request, ct));
    }
}
