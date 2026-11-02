using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizPrep.Api.Domain;

namespace QuizPrep.Api.Features.Import;

[ApiController]
[Route("api/import")]
[Authorize(Roles = Roles.Admin)]
public class ImportController(ImportService import, ILogger<ImportController> logger) : ControllerBase
{
    private const long MaxFileSize = 10 * 1024 * 1024;
    private const int MaxItemsPerCommit = 5000;

    [HttpPost("preview")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFileSize + 1024 * 1024)]
    public async Task<ActionResult<ImportPreviewResponse>> Preview(IFormFile file, [FromForm] int? defaultTopicId, CancellationToken ct)
    {
        if (file.Length == 0) return Problem(title: "File rỗng.", statusCode: StatusCodes.Status400BadRequest);
        if (file.Length > MaxFileSize) return Problem(title: "File tối đa 10 MB.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            // ClosedXML/OpenXml cần stream có thể seek
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            buffer.Position = 0;

            var parsed = Path.GetExtension(file.FileName).ToLowerInvariant() switch
            {
                ".xlsx" => ExcelQuestionParser.Parse(buffer),
                ".docx" => WordQuestionParser.Parse(buffer),
                ".xls" or ".doc" => throw new ImportFormatException("File định dạng cũ (.xls/.doc). Hãy mở bằng Office và lưu lại dạng .xlsx/.docx."),
                _ => throw new ImportFormatException("Chỉ hỗ trợ file Excel (.xlsx) hoặc Word (.docx)."),
            };
            if (parsed.Count == 0)
                throw new ImportFormatException("Không tìm thấy câu hỏi nào. Hãy kiểm tra file có đúng mẫu không.");

            var items = parsed.Select((p, i) => ImportService.FromParsed(p, i + 1)).ToList();
            return new ImportPreviewResponse(file.FileName, await import.AnalyzeAsync(items, defaultTopicId, ct));
        }
        catch (ImportFormatException ex)
        {
            return Problem(title: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Không đọc được file import {FileName}", file.FileName);
            return Problem(title: "Không đọc được file. File có thể bị hỏng hoặc không đúng định dạng.", statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("commit")]
    public async Task<ActionResult<ImportCommitResponse>> Commit(ImportCommitRequest request, CancellationToken ct)
    {
        if (request.Items.Count == 0) return Problem(title: "Chưa chọn câu hỏi nào.", statusCode: StatusCodes.Status400BadRequest);
        if (request.Items.Count > MaxItemsPerCommit)
            return Problem(title: $"Mỗi lần lưu tối đa {MaxItemsPerCommit} câu.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            return await import.CommitAsync(request, ct);
        }
        catch (ImportFormatException ex)
        {
            return Problem(title: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("templates/excel")]
    public FileContentResult ExcelTemplate() =>
        File(ImportTemplates.Excel(), ImportTemplates.ExcelContentType, "mau-cau-hoi.xlsx");

    [HttpGet("templates/word")]
    public FileContentResult WordTemplate() =>
        File(ImportTemplates.Word(), ImportTemplates.WordContentType, "mau-cau-hoi.docx");
}
